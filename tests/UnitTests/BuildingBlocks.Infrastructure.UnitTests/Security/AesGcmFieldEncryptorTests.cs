using System.Security.Cryptography;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Security;

public sealed class AesGcmFieldEncryptorTests
{
    private static readonly string KeyA = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
    private static readonly string KeyB = Convert.ToBase64String(Enumerable.Range(101, 32).Select(i => (byte)i).ToArray());
    private static readonly string BlindKey = Convert.ToBase64String(Enumerable.Range(201, 32).Select(i => (byte)i).ToArray());

    private static AesGcmFieldEncryptor Create(
        string activeKeyId = "k1", Dictionary<string, string>? keys = null, bool allowLegacy = true, string? blindKey = null) =>
        new(Options.Create(new FieldEncryptionOptions
        {
            ActiveKeyId = activeKeyId,
            Keys = keys ?? new() { ["k1"] = KeyA },
            BlindIndexKey = blindKey ?? BlindKey,
            AllowLegacyPlaintext = allowLegacy,
        }));

    [Theory]
    [InlineData("12345678909")]
    [InlineData("")]
    [InlineData("OAB/SP 123.456")]
    [InlineData("João da Silva — ação nº 7 ✓ 日本語")]
    public void EncryptThenDecrypt_ReturnsTheOriginal(string plaintext)
    {
        var encryptor = Create();

        encryptor.Decrypt(encryptor.Encrypt(plaintext, "ctx"), "ctx").Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesTheVersionedFormat_WithoutExposingThePlaintext()
    {
        var stored = Create().Encrypt("12345678909", "ctx");

        stored.Should().StartWith("enc:v1:k1:");
        stored.Should().NotContain("12345678909");
    }

    [Fact]
    public void Encrypt_IsNonDeterministic_SameInputGivesDifferentCiphertexts()
    {
        var encryptor = Create();

        encryptor.Encrypt("12345678909", "ctx").Should().NotBe(encryptor.Encrypt("12345678909", "ctx"));
    }

    [Fact]
    public void Decrypt_WithADifferentContext_Fails()
    {
        var encryptor = Create();
        var stored = encryptor.Encrypt("12345678909", "cliente.documento");

        // Um valor copiado de uma coluna para outra não pode decifrar.
        var act = () => encryptor.Decrypt(stored, "cliente.telefone");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_DetectsTampering()
    {
        var encryptor = Create();
        var stored = encryptor.Encrypt("12345678909", "ctx");
        var parts = stored.Split(':', 4);
        var payload = Convert.FromBase64String(parts[3]);
        payload[^1] ^= 0x01;
        var tampered = $"{parts[0]}:{parts[1]}:{parts[2]}:{Convert.ToBase64String(payload)}";

        var act = () => encryptor.Decrypt(tampered, "ctx");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithAnotherKeyRing_Fails()
    {
        var stored = Create().Encrypt("segredo", "ctx");
        var other = Create(keys: new() { ["k1"] = KeyB });

        var act = () => other.Decrypt(stored, "ctx");

        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("enc:v1:k1")]
    [InlineData("enc:v1:k1:não-é-base64!!")]
    [InlineData("enc:v1:k1:AAAA")]
    [InlineData("enc:v9:k1:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Decrypt_MalformedValues_FailCleanly(string stored)
    {
        var act = () => Create().Decrypt(stored, "ctx");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void KeyRotation_OldValuesStillDecrypt_AndNewOnesUseTheActiveKey()
    {
        var oldStored = Create("k1", new() { ["k1"] = KeyA }).Encrypt("antigo", "ctx");

        var rotated = Create("k2", new() { ["k1"] = KeyA, ["k2"] = KeyB });
        var newStored = rotated.Encrypt("novo", "ctx");

        rotated.Decrypt(oldStored, "ctx").Should().Be("antigo");
        newStored.Should().StartWith("enc:v1:k2:");
        rotated.Decrypt(newStored, "ctx").Should().Be("novo");
    }

    [Fact]
    public void Decrypt_LegacyPlaintext_IsReturnedAsIs_WhenAllowed()
    {
        var encryptor = Create(allowLegacy: true);

        encryptor.Decrypt("12345678909", "ctx").Should().Be("12345678909");
        encryptor.IsEncrypted("12345678909").Should().BeFalse();
    }

    [Fact]
    public void Decrypt_LegacyPlaintext_IsRejected_WhenNotAllowed()
    {
        var act = () => Create(allowLegacy: false).Decrypt("12345678909", "ctx");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void BlindIndex_IsDeterministic_HexSha256Sized_AndHidesTheValue()
    {
        var encryptor = Create();

        var first = encryptor.BlindIndex("12345678909", "ctx");

        first.Should().Be(encryptor.BlindIndex("12345678909", "ctx"));
        first.Should().MatchRegex("^[0-9a-f]{64}$").And.NotContain("12345678909");
        encryptor.BlindIndex("12345678910", "ctx").Should().NotBe(first);
    }

    [Fact]
    public void BlindIndex_DependsOnContext_AndOnTheKey()
    {
        var value = "12345678909";

        Create().BlindIndex(value, "a").Should().NotBe(Create().BlindIndex(value, "b"));
        Create(blindKey: Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 7)).ToArray()))
            .BlindIndex(value, "a").Should().NotBe(Create().BlindIndex(value, "a"));
    }

    [Fact]
    public void BlindIndex_ContextAndValueBoundary_DoesNotCollide()
    {
        var encryptor = Create();

        encryptor.BlindIndex("bc", "a").Should().NotBe(encryptor.BlindIndex("c", "ab"));
    }

    [Theory]
    [InlineData("", "k1")]
    [InlineData("k:1", "k:1")]
    [InlineData("ausente", "k1")]
    public void Constructor_RejectsInvalidActiveKey(string activeKeyId, string ringKeyId)
    {
        var act = () => Create(activeKeyId, new() { [ringKeyId] = KeyA });

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("AAAA")]
    public void Constructor_RejectsKeysThatAreNot32Bytes(string badKey)
    {
        var act = () => Create(keys: new() { ["k1"] = badKey });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_RejectsWeakOrMissingBlindIndexKey()
    {
        ((Action)(() => Create(blindKey: ""))).Should().Throw<InvalidOperationException>();
        ((Action)(() => Create(blindKey: Convert.ToBase64String(new byte[16])))).Should().Throw<InvalidOperationException>();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FieldEncryptionStaticCollection
{
    public const string Name = "FieldEncryption static accessor";
}

/// <summary>
/// Mexe no acessor ESTÁTICO <see cref="FieldEncryption"/> — por isso a collection sem
/// paralelização.
/// </summary>
[Collection(FieldEncryptionStaticCollection.Name)]
public sealed class EncryptionConvertersTests : IDisposable
{
    private readonly AesGcmFieldEncryptor _encryptor = new(Options.Create(new FieldEncryptionOptions
    {
        ActiveKeyId = "k1",
        Keys = new() { ["k1"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()) },
        BlindIndexKey = Convert.ToBase64String(Enumerable.Range(201, 32).Select(i => (byte)i).ToArray()),
    }));

    public EncryptionConvertersTests() => FieldEncryption.Configure(_encryptor);

    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public void EncryptedStringConverter_RoundTripsThroughTheStoreFormat()
    {
        var converter = new EncryptedStringConverter("ctx");

        var stored = (string)converter.ConvertToProvider("11987654321")!;

        stored.Should().StartWith("enc:v1:").And.NotContain("11987654321");
        converter.ConvertFromProvider(stored).Should().Be("11987654321");
    }

    [Fact]
    public void BlindIndexConverter_WritesTheHmac_AndReadsItBackUntouched()
    {
        var converter = new BlindIndexConverter("ctx");

        var stored = (string)converter.ConvertToProvider("12345678909")!;

        stored.Should().Be(_encryptor.BlindIndex("12345678909", "ctx"));
        converter.ConvertFromProvider(stored).Should().Be(stored);
    }

    [Fact]
    public void FieldEncryption_Current_ReturnsTheConfiguredEncryptor()
    {
        // Contrato do acessor: sem encryptor, falha — nunca devolve o valor em claro.
        FieldEncryption.Current.Should().BeSameAs(_encryptor);
    }
}

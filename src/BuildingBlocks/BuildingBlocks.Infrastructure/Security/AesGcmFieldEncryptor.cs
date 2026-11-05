using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Advocacia.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// AES-256-GCM. Formato gravado: <c>enc:v1:{keyId}:{base64(nonce[12] | tag[16] | ciphertext)}</c>.
///   - Nonce aleatório de 96 bits por valor (GCM exige nonce único por chave; com 2^32 valores
///     por chave a probabilidade de colisão é desprezível, e a rotação limita isso).
///   - Tag de 128 bits: qualquer alteração do texto cifrado, do keyId ou do contexto faz a
///     decifragem falhar (integridade, não só sigilo).
///   - O keyId no prefixo permite rotação sem big-bang: decifra com a chave que cifrou.
/// </summary>
public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    private const string Prefix = "enc:";
    private const string Version = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int MinimumBlindIndexKeySize = 32;

    private readonly string _activeKeyId;
    private readonly Dictionary<string, byte[]> _keys;
    private readonly byte[] _blindIndexKey;
    private readonly bool _allowLegacyPlaintext;

    public AesGcmFieldEncryptor(IOptions<FieldEncryptionOptions> options)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ActiveKeyId) || settings.ActiveKeyId.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Encryption:ActiveKeyId ausente ou inválido (não pode ser vazio nem conter ':').");
        }

        _keys = [];
        foreach (var (id, base64) in settings.Keys)
        {
            _keys[id] = DecodeKey(base64, KeySize, $"Encryption:Keys:{id}");
        }

        if (!_keys.ContainsKey(settings.ActiveKeyId))
        {
            throw new InvalidOperationException(
                $"Encryption:ActiveKeyId '{settings.ActiveKeyId}' não existe em Encryption:Keys.");
        }

        _blindIndexKey = DecodeKey(settings.BlindIndexKey, MinimumBlindIndexKeySize, "Encryption:BlindIndexKey", exact: false);
        _activeKeyId = settings.ActiveKeyId;
        _allowLegacyPlaintext = settings.AllowLegacyPlaintext;
    }

    public string Encrypt(string plaintext, string context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrEmpty(context);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSize + TagSize + plainBytes.Length];

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_keys[_activeKeyId], TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag, Encoding.UTF8.GetBytes(context));

        return $"{Prefix}{Version}:{_activeKeyId}:{Convert.ToBase64String(payload)}";
    }

    public string Decrypt(string stored, string context)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentException.ThrowIfNullOrEmpty(context);

        if (!IsEncrypted(stored))
        {
            return _allowLegacyPlaintext
                ? stored
                : throw new CryptographicException("Valor em texto puro encontrado numa coluna cifrada.");
        }

        // enc : v1 : keyId : payload
        var parts = stored.Split(':', 4);
        if (parts.Length != 4 || parts[1] != Version)
        {
            throw new CryptographicException("Formato de valor cifrado não reconhecido.");
        }

        if (!_keys.TryGetValue(parts[2], out var key))
        {
            throw new CryptographicException($"Chave de criptografia '{parts[2]}' não está no anel de chaves.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Valor cifrado corrompido.", ex);
        }

        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Valor cifrado corrompido.");
        }

        var plainBytes = new byte[payload.Length - NonceSize - TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            payload.AsSpan(0, NonceSize),
            payload.AsSpan(NonceSize + TagSize),
            payload.AsSpan(NonceSize, TagSize),
            plainBytes,
            Encoding.UTF8.GetBytes(context));

        return Encoding.UTF8.GetString(plainBytes);
    }

    public string BlindIndex(string value, string context)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(context);

        // O separador NUL impede colisão entre ("a","bc") e ("ab","c").
        var data = Encoding.UTF8.GetBytes(context + '\0' + value);
        return Convert.ToHexStringLower(HMACSHA256.HashData(_blindIndexKey, data));
    }

    public bool IsEncrypted(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

    private static byte[] DecodeKey(string base64, int size, string name, bool exact = true)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64 ?? string.Empty);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{name} não é Base64 válido.", ex);
        }

        if (exact ? bytes.Length != size : bytes.Length < size)
        {
            throw new InvalidOperationException(
                $"{name} deve ter {(exact ? "exatamente" : "no mínimo")} {size} bytes (Base64). Gere com: openssl rand -base64 {size}.");
        }

        return bytes;
    }
}

using System.Text;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Security;

public sealed class WebhookSignatureValidatorTests
{
    private const string Secret = "app-secret-de-teste";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"entry\":[{\"id\":\"1\"}]}");

    [Fact]
    public void KnownVector_MatchesTheMetaAlgorithm()
    {
        // HMAC-SHA256("key", "The quick brown fox jumps over the lazy dog") — vetor público.
        var header = "sha256=f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8";

        WebhookSignatureValidator.IsValid(Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog"), header, "key")
            .Should().BeTrue();
    }

    [Fact]
    public void ValidSignature_IsAccepted() =>
        WebhookSignatureValidator.IsValid(Body, WebhookSignatureValidator.Sign(Body, Secret), Secret).Should().BeTrue();

    [Fact]
    public void TamperedBody_IsRejected()
    {
        var header = WebhookSignatureValidator.Sign(Body, Secret);

        WebhookSignatureValidator.IsValid(Encoding.UTF8.GetBytes("{\"entry\":[]}"), header, Secret).Should().BeFalse();
    }

    [Fact]
    public void WrongSecret_IsRejected() =>
        WebhookSignatureValidator.IsValid(Body, WebhookSignatureValidator.Sign(Body, "outro"), Secret).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sha256=")]
    [InlineData("sha256=nao-e-hex")]
    [InlineData("sha256=abcd")]
    [InlineData("sha1=0123456789abcdef0123456789abcdef01234567")]
    [InlineData("0123456789abcdef")]
    public void MissingOrMalformedHeaders_AreRejected(string? header) =>
        WebhookSignatureValidator.IsValid(Body, header, Secret).Should().BeFalse();

    [Fact]
    public void EmptySecret_ThrowsInsteadOfAcceptingEverything()
    {
        var act = () => WebhookSignatureValidator.IsValid(Body, "sha256=00", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HeaderPrefixAndHexCase_AreTolerated() =>
        WebhookSignatureValidator.IsValid(Body, WebhookSignatureValidator.Sign(Body, Secret).ToUpperInvariant(), Secret).Should().BeTrue();
}

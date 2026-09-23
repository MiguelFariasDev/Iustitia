using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Identity;

public class EmailTests
{
    [Theory]
    [InlineData("advogada@escritorio.com.br", "advogada@escritorio.com.br")]
    [InlineData("  Advogada@Escritorio.COM.BR  ", "advogada@escritorio.com.br")]
    public void Create_WhenValid_NormalizesToLowercaseTrimmed(string input, string expected)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNullOrWhitespace_Fails(string? email)
    {
        var result = Email.Create(email);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_EMAIL_INVALID));
    }

    [Theory]
    [InlineData("sem-arroba.com")]
    [InlineData("sem-dominio@")]
    [InlineData("@sem-usuario.com")]
    [InlineData("espaco no meio@dominio.com")]
    public void Create_WhenInvalidFormat_Fails(string email)
    {
        var result = Email.Create(email);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_EMAIL_INVALID));
    }

    [Fact]
    public void Equals_WhenSameNormalizedValue_ReturnsTrue()
    {
        var first = Email.Create("Nome@Dominio.com").Value;
        var second = Email.Create("nome@dominio.com").Value;

        first.Should().Be(second);
    }
}

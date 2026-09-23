using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Tenancy;

public class CNPJTests
{
    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void Create_WhenValidCnpj_Succeeds(string cnpj)
    {
        var result = CNPJ.Create(cnpj);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("11222333000181");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNullOrWhitespace_Fails(string? cnpj)
    {
        var result = CNPJ.Create(cnpj);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CNPJ_INVALID));
    }

    [Fact]
    public void Create_WhenWrongLength_Fails()
    {
        var result = CNPJ.Create("123456");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CNPJ_INVALID));
    }

    [Fact]
    public void Create_WhenAllDigitsEqual_Fails()
    {
        var result = CNPJ.Create("11111111111111");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CNPJ_INVALID));
    }

    [Fact]
    public void Create_WhenCheckDigitsInvalid_Fails()
    {
        var result = CNPJ.Create("11222333000180");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CNPJ_INVALID));
    }

    [Fact]
    public void Equals_WhenSameDigits_ReturnsTrue()
    {
        var first = CNPJ.Create("11.222.333/0001-81").Value;
        var second = CNPJ.Create("11222333000181").Value;

        first.Should().Be(second);
    }
}

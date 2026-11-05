using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;

namespace Advocacia.Platform.Domain.UnitTests.Tenancy;

public class CPFTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void Create_WhenValidCpf_KeepsOnlyDigits(string cpf)
    {
        var result = CPF.Create(cpf);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("52998224725");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("529.982.247-24")] // dígito verificador errado
    [InlineData("111.111.111-11")] // dígitos repetidos passam na conta, mas não são CPF
    [InlineData("000.000.000-00")]
    [InlineData("5299822472512")] // dígitos demais
    public void Create_WhenInvalid_Fails(string? cpf)
    {
        var result = CPF.Create(cpf);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CPF_INVALID));
    }

    [Fact]
    public void ToString_NeverExposesTheDocument()
    {
        CPF.Create("529.982.247-25").Value.ToString().Should().NotContain("529");
    }
}

using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public sealed class LogMaskingPolicyTests
{
    [Fact]
    public void Mask_MascaraCpf()
    {
        LogMaskingPolicy.Mask("CPF: 123.456.789-01").Should().Be("CPF: ***.***.***-**");
    }

    [Fact]
    public void Mask_MascaraCnpj()
    {
        LogMaskingPolicy.Mask("CNPJ: 12.345.678/0001-99").Should().Be("CNPJ: **.***.***/****-**");
    }

    [Fact]
    public void Mask_MascaraEmail()
    {
        LogMaskingPolicy.Mask("contato: usuario@dominio.com.br").Should().Be("contato: us***@dominio.com.br");
    }

    [Fact]
    public void Mask_MascaraTelefone_MantendoDdd()
    {
        LogMaskingPolicy.Mask("tel: (11) 98765-4321").Should().Be("tel: (11) *****-****");
    }

    [Fact]
    public void Mask_MascaraBearerToken()
    {
        LogMaskingPolicy.Mask("Authorization: Bearer abc123.def456-ghi").Should().Be("Authorization: Bearer ***");
    }

    [Fact]
    public void Mask_WhenNoSensitiveData_ReturnsUnchanged()
    {
        const string value = "requisição concluída com sucesso";
        LogMaskingPolicy.Mask(value).Should().Be(value);
    }

    [Fact]
    public void Mask_WhenNullOrEmpty_ReturnsEmpty()
    {
        LogMaskingPolicy.Mask(null).Should().Be(string.Empty);
        LogMaskingPolicy.Mask(string.Empty).Should().Be(string.Empty);
    }
}

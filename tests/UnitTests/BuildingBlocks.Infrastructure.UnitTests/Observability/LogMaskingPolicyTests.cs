using Advocacia.BuildingBlocks.Infrastructure.Observability;
using FluentAssertions;

namespace Advocacia.BuildingBlocks.Infrastructure.UnitTests.Observability;

public sealed class LogMaskingPolicyTests
{
    [Fact]
    public void Mask_Cpf_IsFullyMasked()
    {
        // Deliberadamente NÃO é "***.456.789-**": ver comentário de LogMaskingPolicy.
        LogMaskingPolicy.Mask("CPF: 123.456.789-01").Should().Be("CPF: ***.***.***-**");
    }

    [Fact]
    public void Mask_Cnpj_KeepsMiddleAndBranch()
    {
        LogMaskingPolicy.Mask("CNPJ: 12.345.678/0001-99").Should().Be("CNPJ: **.345.678/0001-**");
    }

    [Fact]
    public void Mask_Email_KeepsFirstCharacterAndDomain()
    {
        LogMaskingPolicy.Mask("contato: miguel@dominio.com.br").Should().Be("contato: m***@dominio.com.br");
    }

    [Theory]
    [InlineData("tel: (11) 98765-1234", "tel: (11) 9****-1234")]
    [InlineData("tel: (11) 3456-1234", "tel: (11) 3***-1234")]
    [InlineData("tel: (11)98765-1234", "tel: (11)9****-1234")]
    [InlineData("tel: +55 (11) 98765-1234", "tel: +55 (11) 9****-1234")]
    [InlineData("tel: 11 98765-1234", "tel: 11 9****-1234")]
    public void Mask_Phone_KeepsDddFirstDigitAndLastFour(string input, string expected)
    {
        LogMaskingPolicy.Mask(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("doc=12345678909", "doc=***********")]
    [InlineData("doc 12345678000195 fim", "doc ************** fim")]
    [InlineData("cel 11987654321", "cel ***********")]
    public void Mask_UnformattedDocumentsAndPhones_AreFullyMasked(string input, string expected)
    {
        LogMaskingPolicy.Mask(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Authorization: Bearer abc123.def456-ghi", "Authorization: Bearer ***")]
    [InlineData("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.c2lnbmF0dXJl fim", "jwt *** fim")]
    [InlineData("GET /cb?password=hunter2&x=1", "GET /cb?password=***&x=1")]
    [InlineData("{\"senha\":\"Segredo123!\",\"nome\":\"Ana\"}", "{\"senha\":\"***\",\"nome\":\"Ana\"}")]
    [InlineData("access_token=abc.def", "access_token=***")]
    [InlineData("apikey: sk-123", "apikey: ***")]
    public void Mask_SecretsAndTokens_AreRedacted(string input, string expected)
    {
        LogMaskingPolicy.Mask(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("processo 0001234-56.2024.8.26.0100 distribuído")]
    [InlineData("tenant 3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("requisição concluída com sucesso em 123ms")]
    [InlineData("12345678901234567890")]
    public void Mask_NonSensitiveText_IsLeftAlone(string value)
    {
        LogMaskingPolicy.Mask(value).Should().Be(value);
    }

    [Fact]
    public void Mask_SeveralSensitiveValuesInOneString_AreAllMasked()
    {
        var masked = LogMaskingPolicy.Mask("cliente 123.456.789-01 <ana@x.com> (11) 98765-4321");

        masked.Should().NotContain("123.456").And.NotContain("ana@").And.NotContain("98765");
    }

    [Fact]
    public void Mask_WhenNullOrEmpty_ReturnsEmpty()
    {
        LogMaskingPolicy.Mask(null).Should().Be(string.Empty);
        LogMaskingPolicy.Mask(string.Empty).Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("Password", true)]
    [InlineData("userPassword", true)]
    [InlineData("AccessToken", true)]
    [InlineData("Authorization", true)]
    [InlineData("Cpf", true)]
    [InlineData("Documento", true)]
    [InlineData("OabNumber", true)]
    [InlineData("Path", false)]
    [InlineData("TenantId", false)]
    [InlineData("StatusCode", false)]
    public void IsSensitivePropertyName_FlagsCredentialsAndDocuments(string name, bool expected)
    {
        LogMaskingPolicy.IsSensitivePropertyName(name).Should().Be(expected);
    }
}

using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class TransportGuardTests
{
    [Theory]
    [InlineData("https://ollama.exemplo.com.br")]
    [InlineData("http://localhost:11434")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("https://203.0.113.10:8443")]
    public void AceitaTlsEHostLocal(string url)
    {
        new OllamaOptions { BaseUrl = url }.Validate().Should().BeNull();
    }

    [Fact]
    public void RecusaHttpParaHostPublico_AMenosQueSejaAceitoExplicitamente()
    {
        var recusa = new OllamaOptions { BaseUrl = "http://203.0.113.10:41000" }.Validate();
        var aceito = new OllamaOptions { BaseUrl = "http://203.0.113.10:41000", AllowInsecureHttp = true }.Validate();

        recusa.Should().Contain("https://").And.Contain("AllowInsecureHttp");
        aceito.Should().BeNull();
    }

    [Fact]
    public void RecusaChaveSobTransporteInseguro_MesmoComOOptIn()
    {
        var problema = new QdrantOptions
        {
            BaseUrl = "http://203.0.113.10:6334", ApiKey = "chave", AllowInsecureHttp = true,
        }.Validate();

        problema.Should().Contain("texto claro");
    }

    [Theory]
    [InlineData("IP_PUBLICO_DA_VAST:PORTA")]
    [InlineData("http://IP_PUBLICO_DA_VAST:PORTA_EXTERNA")]
    [InlineData("ftp://servidor")]
    [InlineData("")]
    public void RecusaUrlInvalidaOuPlaceholderNaoSubstituido(string url)
    {
        new OllamaOptions { BaseUrl = url }.Validate().Should().Contain("inválida");
    }
}

using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Integrations;

/// <summary>Adaptador do juscraper: mapeamento de erros (seção 4) e montagem do movimento.</summary>
public sealed class JuscraperConsultaProcessoTribunalTests
{
    private static readonly CNJNumber Numero = CNJNumber.Create("1234567-48.2026.8.26.0001").Value;

    private readonly JuscraperStub _stub = new();

    private JuscraperConsultaProcessoTribunal Adaptador() =>
        new(new StubHttpClientFactory(_stub), NullLogger<JuscraperConsultaProcessoTribunal>.Instance);

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "NUMERO_CNJ_INVALIDO", nameof(ErrorCode.CNJ_INVALID_REQUEST))]
    [InlineData(HttpStatusCode.UnprocessableEntity, "TRIBUNAL_NAO_SUPORTADO", nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED))]
    [InlineData(HttpStatusCode.ServiceUnavailable, "CAPTCHA_EXIGIDO", nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED))]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ACESSO_BLOQUEADO", nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED))]
    [InlineData(HttpStatusCode.BadGateway, "TRIBUNAL_INDISPONIVEL", nameof(ErrorCode.CNJ_UNAVAILABLE))]
    [InlineData(HttpStatusCode.BadGateway, "RESPOSTA_INESPERADA", nameof(ErrorCode.CNJ_INVALID_RESPONSE))]
    [InlineData(HttpStatusCode.Unauthorized, "NAO_AUTORIZADO", nameof(ErrorCode.CNJ_AUTH_FAILED))]
    public async Task MapeiaCadaCodigoDoJuscraper(HttpStatusCode status, string codigo, string esperado)
    {
        _stub.Problema(status, codigo);

        var consulta = await Adaptador().ConsultarDetalhadoAsync(Numero, CancellationToken.None);

        consulta.Resultado.Error.Code.Should().Be(esperado);
        consulta.Falha!.Motivo.Should().Be(codigo, "o código original fica disponível para a decisão de fallback");
    }

    [Fact]
    public async Task Timeout_ViraCnjTimeout()
    {
        _stub.Lanca(new TaskCanceledException());

        var consulta = await Adaptador().ConsultarDetalhadoAsync(Numero, CancellationToken.None);

        consulta.Resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TIMEOUT));
        consulta.Falha!.Transitoria.Should().BeTrue();
    }

    [Fact]
    public async Task JsonIlegivel_ViraCnjInvalidResponse()
    {
        _stub.Json("<html>manutenção</html>");

        var consulta = await Adaptador().ConsultarDetalhadoAsync(Numero, CancellationToken.None);

        consulta.Resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_INVALID_RESPONSE));
    }

    [Theory]
    [InlineData("Expedido Certidão", "Certidão de Remessa", "Expedido Certidão — Certidão de Remessa")]
    [InlineData("Sentença", "sentença", "Sentença")]
    [InlineData("Juntada", null, "Juntada")]
    [InlineData("Juntada", "  ", "Juntada")]
    public void NomeDoMovimento_LevaOComplemento(string nome, string? complemento, string esperado) =>
        JuscraperConsultaProcessoTribunal.NomeComComplemento(nome, complemento).Should().Be(esperado);

    [Fact]
    public void ComplementoLongo_ECortadoEm300Caracteres()
    {
        var nome = JuscraperConsultaProcessoTribunal.NomeComComplemento("Decisão", new string('x', 500));

        nome.Should().Be("Decisão — " + new string('x', 300) + "…");
    }
}

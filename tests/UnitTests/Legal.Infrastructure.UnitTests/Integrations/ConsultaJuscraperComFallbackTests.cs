using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly.CircuitBreaker;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Integrations;

/// <summary>Seção 5 do contrato: juscraper como fonte principal, DataJud como fallback.</summary>
public sealed class ConsultaJuscraperComFallbackTests
{
    private static readonly CNJNumber Numero = CNJNumber.Create("1234567-48.2026.8.26.0001").Value;

    private const string Encontrado = """
        {
          "numeroProcesso": "1234567-48.2026.8.26.0001", "tribunal": "TJSP", "encontrado": true,
          "processos": [
            {
              "numeroProcesso": "1234567-48.2026.8.26.0001", "tribunal": "TJSP", "grau": "G2", "sistema": "e-SAJ",
              "dataAjuizamento": "2026-08-01", "emSegredoDeJustica": false, "classe": "Apelação Cível",
              "orgaoJulgador": "3ª Câmara", "assuntos": ["Recurso"],
              "movimentos": [ { "id": "g2", "nome": "Distribuído", "dataHora": "2026-11-20T10:00:00-03:00", "complemento": null, "possuiDocumento": false, "instancia": "G2" } ],
              "codigoMunicipioIbge": null, "movimentosCompletos": false
            },
            {
              "numeroProcesso": "1234567-48.2026.8.26.0001", "tribunal": "TJSP", "grau": "G1", "sistema": "e-SAJ",
              "dataAjuizamento": "2026-06-02", "emSegredoDeJustica": false, "classe": "Procedimento Comum Cível",
              "orgaoJulgador": "2ª Vara Cível", "assuntos": ["Perdas e Danos"],
              "movimentos": [
                { "id": "a", "nome": "Expedido Certidão", "dataHora": "2026-09-22T00:00:00-03:00", "complemento": "Certidão de Remessa da Intimação ao Portal", "possuiDocumento": true, "instancia": "G1" },
                { "id": "b", "nome": "Sentença", "dataHora": "2026-11-21T00:00:00-03:00", "complemento": "Sentença", "possuiDocumento": true, "instancia": "G1" }
              ],
              "codigoMunicipioIbge": 3550308, "movimentosCompletos": false
            }
          ],
          "consultadoEm": "2026-11-25T17:07:58.66+00:00", "avisos": []
        }
        """;

    private const string NaoEncontrado = """
        { "numeroProcesso": "1234567-48.2026.8.26.0001", "tribunal": "TJSP", "encontrado": false, "processos": [], "consultadoEm": "2026-11-25T17:07:58+00:00", "avisos": [] }
        """;

    private static readonly ProcessoTribunal DoDataJud = new(
        "1234567-48.2026.8.26.0001", "TJSP", "G1", null, false, "Classe CNJ", "PJe", "Vara", [], [], null, FonteConsulta.DataJud);

    private readonly JuscraperStub _juscraper = new();
    private readonly IConsultaProcessoTribunal _dataJud = Substitute.For<IConsultaProcessoTribunal>();

    public ConsultaJuscraperComFallbackTests() =>
        _dataJud.ConsultarAsync(Arg.Any<CNJNumber>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ProcessoTribunal?>(DoDataJud));

    private ConsultaProcessoTribunalComFallback Composto(JuscraperOptions? options = null) =>
        new(
            new JuscraperConsultaProcessoTribunal(new StubHttpClientFactory(_juscraper), NullLogger<JuscraperConsultaProcessoTribunal>.Instance),
            _dataJud,
            Options.Create(options ?? new JuscraperOptions { BaseUrl = "http://juscraper.test/" }),
            new ConsultaTribunalTelemetry(),
            NullLogger<ConsultaProcessoTribunalComFallback>.Instance);

    [Fact]
    public async Task JuscraperEncontra_UsaOJuscraper_ESemChamarODataJud()
    {
        _juscraper.Json(Encontrado);

        var resultado = await Composto().ConsultarAsync(Numero);

        var processo = resultado.Value!;
        processo.Fonte.Should().Be(FonteConsulta.Juscraper);
        processo.Grau.Should().Be("G1", "os metadados são do 1º grau quando ele existe");
        processo.OrgaoJulgador.Should().Be("2ª Vara Cível");
        processo.Movimentos.Select(m => m.Nome).Should().Equal(
            "Sentença", "Distribuído", "Expedido Certidão — Certidão de Remessa da Intimação ao Portal");
        _juscraper.Caminhos.Single().Should().Be("/v1/processos/12345674820268260001?max_movimentacoes=10");
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, default);
    }

    [Fact]
    public async Task NaoEncontradoNoPortal_DevolveNaoEncontrado_SemDataJud()
    {
        _juscraper.Json(NaoEncontrado);

        var resultado = await Composto().ConsultarAsync(Numero);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, default);
    }

    [Fact]
    public async Task NaoEncontrado_ComFallbackQuandoNaoEncontrado_ConsultaODataJud()
    {
        _juscraper.Json(NaoEncontrado);

        var resultado = await Composto(new JuscraperOptions { BaseUrl = "http://j/", FallbackQuandoNaoEncontrado = true }).ConsultarAsync(Numero);

        resultado.Value.Should().Be(DoDataJud);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "TRIBUNAL_NAO_SUPORTADO")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "CAPTCHA_EXIGIDO")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ACESSO_BLOQUEADO")]
    [InlineData(HttpStatusCode.BadGateway, "TRIBUNAL_INDISPONIVEL")]
    [InlineData(HttpStatusCode.BadGateway, "RESPOSTA_INESPERADA")]
    [InlineData(HttpStatusCode.Unauthorized, "NAO_AUTORIZADO")]
    public async Task CodigosDeFallback_UsamODataJud(HttpStatusCode status, string codigo)
    {
        _juscraper.Problema(status, codigo);

        var resultado = await Composto().ConsultarAsync(Numero);

        resultado.Value.Should().Be(DoDataJud);
        await _dataJud.Received(1).ConsultarAsync(Numero, Arg.Any<CancellationToken>());
    }

    public static TheoryData<Exception> FalhasDeTransporte() =>
        [new TaskCanceledException("timeout"), new HttpRequestException("recusada"), new BrokenCircuitException()];

    [Theory]
    [MemberData(nameof(FalhasDeTransporte))]
    public async Task TimeoutRedeECircuitoAberto_UsamODataJud(Exception falha)
    {
        _juscraper.Lanca(falha);

        var resultado = await Composto().ConsultarAsync(Numero);

        resultado.Value.Should().Be(DoDataJud);
    }

    [Fact]
    public async Task Erro500SemProblemDetails_UsaODataJud()
    {
        _juscraper.Status(HttpStatusCode.InternalServerError);

        (await Composto().ConsultarAsync(Numero)).Value.Should().Be(DoDataJud);
    }

    [Fact]
    public async Task NumeroInvalido_NaoChamaODataJud()
    {
        _juscraper.Problema(HttpStatusCode.BadRequest, "NUMERO_CNJ_INVALIDO");

        var resultado = await Composto().ConsultarAsync(Numero);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_INVALID_REQUEST));
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, default);
    }

    [Fact]
    public async Task AsDuasFontesFalham_DevolveOErroDoDataJud()
    {
        _juscraper.Problema(HttpStatusCode.BadGateway, "TRIBUNAL_INDISPONIVEL");
        _dataJud.ConsultarAsync(Arg.Any<CNJNumber>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ProcessoTribunal?>(ErrorFactory.From(ErrorCode.CNJ_RATE_LIMITED)));

        var resultado = await Composto().ConsultarAsync(Numero);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_RATE_LIMITED));
    }

    [Fact]
    public async Task FallbackDataJudDesligado_DevolveOErroDoJuscraper()
    {
        _juscraper.Problema(HttpStatusCode.UnprocessableEntity, "TRIBUNAL_NAO_SUPORTADO");

        var resultado = await Composto(new JuscraperOptions { BaseUrl = "http://j/", FallbackDataJud = false }).ConsultarAsync(Numero);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED));
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, default);
    }

    [Fact]
    public async Task BaseUrlVazio_SoDataJud()
    {
        var resultado = await Composto(new JuscraperOptions { BaseUrl = "" }).ConsultarAsync(Numero);

        resultado.Value.Should().Be(DoDataJud);
        _juscraper.Caminhos.Should().BeEmpty();
    }
}

using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.CNJ.Application.Abstractions;
using Advocacia.Modules.Integrations.CNJ.Application.Models.DataJud;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Acompanhamento;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Integrations;

/// <summary>Seção 6.4: no acompanhamento, o processo fica numa fonte só.</summary>
public sealed class AcompanhamentoComFallbackTests
{
    private static readonly CNJNumber Numero = CNJNumber.Create("1234567-48.2026.8.26.0001").Value;

    private const string Movimentacoes = """
        {
          "numeroProcesso": "1234567-48.2026.8.26.0001", "tribunal": "TJSP", "encontrado": true, "completas": true,
          "movimentos": [
            { "id": "7688399a5b0f87f281013cbf7a92c58f", "nome": "Arquivado Definitivamente", "dataHora": "2026-09-22T00:00:00-03:00", "complemento": null, "possuiDocumento": false, "instancia": "G1" },
            { "id": null, "nome": "Sem id", "dataHora": "2026-09-21T00:00:00-03:00", "complemento": null, "possuiDocumento": false, "instancia": "G1" }
          ],
          "consultadoEm": "2026-11-25T17:08:00.23+00:00", "avisos": []
        }
        """;

    private readonly JuscraperStub _juscraper = new();
    private readonly IDataJudClient _dataJud = Substitute.For<IDataJudClient>();

    public AcompanhamentoComFallbackTests() =>
        _dataJud.ConsultarProcessoAsync("TJSP", Numero.Digits, Arg.Any<CancellationToken>()).Returns(Result.Success<DataJudProcesso?>(
            new DataJudProcesso(
                Numero.Digits, "TJSP", "G1", null, 0, null, null, null, null, [],
                [
                    new DataJudMovimento(26, "Distribuição", new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero)),
                    new DataJudMovimento(51, "Sentença", new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero)),
                ])));

    private AcompanhamentoProcessualComFallback Composto(JuscraperOptions? options = null) => new(
        new JuscraperAcompanhamento(new StubHttpClientFactory(_juscraper), NullLogger<JuscraperAcompanhamento>.Instance),
        new DataJudAcompanhamento(_dataJud),
        Options.Create(options ?? new JuscraperOptions { BaseUrl = "http://juscraper.test/" }),
        NullLogger<AcompanhamentoProcessualComFallback>.Instance);

    [Fact]
    public async Task Juscraper_UsaOIdDoServico_EIgnoraAndamentoSemId()
    {
        _juscraper.Json(Movimentacoes);

        var resultado = await Composto().ObterAsync(Numero, null, new DateOnly(2026, 9, 1));

        resultado.Value.Fonte.Should().Be(FonteConsulta.Juscraper);
        resultado.Value.Andamentos.Should().ContainSingle().Which.ExternalId.Should().Be("7688399a5b0f87f281013cbf7a92c58f");
        _juscraper.Caminhos.Single().Should().Be("/v1/processos/12345674820268260001/movimentacoes?desde=2026-09-01");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "TRIBUNAL_INDISPONIVEL")]
    [InlineData(HttpStatusCode.BadGateway, "RESPOSTA_INESPERADA")]
    public async Task ErroDoJuscraperQueNaoESemCobertura_NaoUsaFallback(HttpStatusCode status, string codigo)
    {
        _juscraper.Problema(status, codigo);

        var resultado = await Composto().ObterAsync(Numero, null, null);

        resultado.IsFailure.Should().BeTrue();
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarProcessoAsync(default!, default!, default);
    }

    [Fact]
    public async Task Timeout_NaoUsaFallback()
    {
        _juscraper.Lanca(new TaskCanceledException());

        var resultado = await Composto().ObterAsync(Numero, null, null);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TIMEOUT));
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarProcessoAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "TRIBUNAL_NAO_SUPORTADO")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "CAPTCHA_EXIGIDO")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ACESSO_BLOQUEADO")]
    public async Task TribunalSemCobertura_AcompanhaPeloDataJud_ComIdPrefixado(HttpStatusCode status, string codigo)
    {
        _juscraper.Problema(status, codigo);

        var resultado = await Composto().ObterAsync(Numero, null, new DateOnly(2026, 11, 1));

        resultado.Value.Fonte.Should().Be(FonteConsulta.DataJud);
        var andamento = resultado.Value.Andamentos.Should().ContainSingle("o desde filtra a distribuição de junho").Subject;
        andamento.Nome.Should().Be("Sentença");
        andamento.ExternalId.Should().StartWith("datajud:").And.HaveLength(64);
    }

    [Fact]
    public void IdDoDataJud_EEstavelEntreConsultas()
    {
        var movimento = new DataJudMovimento(51, "Sentença", new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

        DataJudAcompanhamento.Id(movimento).Should().Be(DataJudAcompanhamento.Id(movimento with { }));
        DataJudAcompanhamento.Id(movimento).Should().NotBe(DataJudAcompanhamento.Id(movimento with { Codigo = 52 }));
    }

    [Fact]
    public async Task ProcessoNoDataJud_ContinuaNoDataJud_SemConsultarOJuscraper()
    {
        var resultado = await Composto().ObterAsync(Numero, FonteConsulta.DataJud, null);

        resultado.Value.Fonte.Should().Be(FonteConsulta.DataJud);
        _juscraper.Caminhos.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessoNoJuscraper_QuePerdeuCobertura_NaoMigraSozinhoParaODataJud()
    {
        _juscraper.Problema(HttpStatusCode.ServiceUnavailable, "CAPTCHA_EXIGIDO");

        var resultado = await Composto().ObterAsync(Numero, FonteConsulta.Juscraper, null);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED));
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarProcessoAsync(default!, default!, default);
    }

    [Fact]
    public async Task FallbackDataJudDesligado_TribunalSemCoberturaFicaSemFonte()
    {
        _juscraper.Problema(HttpStatusCode.UnprocessableEntity, "TRIBUNAL_NAO_SUPORTADO");

        var resultado = await Composto(new JuscraperOptions { BaseUrl = "http://j/", FallbackDataJud = false }).ObterAsync(Numero, null, null);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED));
        await _dataJud.DidNotReceiveWithAnyArgs().ConsultarProcessoAsync(default!, default!, default);
    }
}

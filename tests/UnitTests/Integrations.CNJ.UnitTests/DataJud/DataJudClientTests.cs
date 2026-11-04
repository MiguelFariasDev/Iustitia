using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;
using Advocacia.Modules.Integrations.CNJ.UnitTests.Djen;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.DataJud;

public class DataJudClientTests
{
    private const string NumeroProcesso = "00008323520184013202";

    /// <summary>
    /// Resposta REAL da API do DataJud (TRF1), recortada. Note "dataAjuizamento":
    /// "20181029000000" — formato compacto, diferente do ISO que a documentação mostra e que
    /// os movimentos usam. Esse desencontro derrubou a primeira versão do cliente.
    /// </summary>
    private const string RespostaEncontrado = """
        {
          "took": 6679,
          "timed_out": false,
          "hits": {
            "total": { "value": 1, "relation": "eq" },
            "hits": [
              {
                "_id": "TRF1_436_JE_16403_00008323520184013202",
                "_index": "api_publica_trf1",
                "_source": {
                  "numeroProcesso": "00008323520184013202",
                  "tribunal": "TRF1",
                  "grau": "JE",
                  "dataAjuizamento": "20181029000000",
                  "nivelSigilo": 0,
                  "classe": { "codigo": 436, "nome": "Procedimento do Juizado Especial Cível" },
                  "sistema": { "codigo": 1, "nome": "Pje" },
                  "formato": { "codigo": 1, "nome": "Eletrônico" },
                  "orgaoJulgador": { "codigo": 16403, "nome": "JEF Adj - Tefé", "codigoMunicipioIBGE": 5128 },
                  "assuntos": [ { "codigo": 6177, "nome": "Concessão" } ],
                  "movimentos": [
                    { "codigo": 26, "nome": "Distribuição", "dataHora": "2018-10-30T14:06:24.000Z" },
                    { "codigo": 51, "nome": "Sentença", "dataHora": "2019-03-15T09:00:00.000Z" }
                  ]
                }
              }
            ]
          }
        }
        """;

    private const string RespostaVazia = """
        { "took": 12, "timed_out": false, "hits": { "total": { "value": 0, "relation": "eq" }, "hits": [] } }
        """;

    private static (DataJudClient Client, StubHttpMessageHandler Handler, FakeCacheService Cache) CreateClient(
        Action<StubHttpMessageHandler> arrange)
    {
        var handler = new StubHttpMessageHandler();
        arrange(handler);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-publica.datajud.cnj.jus.br") };
        var cache = new FakeCacheService();
        var options = Options.Create(new DataJudOptions { CacheTtlMinutes = 60 });

        return (new DataJudClient(httpClient, cache, new DataJudTelemetry(), options, NullLogger<DataJudClient>.Instance), handler, cache);
    }

    [Fact]
    public async Task ConsultarProcesso_QuandoEncontrado_MapeiaTodosOsMetadados()
    {
        var (client, handler, _) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, RespostaEncontrado));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var processo = result.Value;
        processo.Should().NotBeNull();
        processo!.Tribunal.Should().Be("TRF1");
        processo.Grau.Should().Be("JE");
        processo.Classe!.Nome.Should().Be("Procedimento do Juizado Especial Cível");
        processo.Sistema!.Nome.Should().Be("Pje");
        processo.OrgaoJulgador!.Nome.Should().Be("JEF Adj - Tefé");
        processo.AssuntoPrincipal!.Nome.Should().Be("Concessão");
        processo.EmSegredoDeJustica.Should().BeFalse();

        handler.ReceivedUris.Should().ContainSingle()
            .Which.AbsolutePath.Should().Be("/api_publica_trf1/_search");
    }

    /// <summary>
    /// O DataJud devolve dataAjuizamento em yyyyMMddHHmmss e dataHora em ISO 8601 — na mesma
    /// resposta. Um cliente que só entenda ISO falha na leitura de um 200 perfeitamente válido.
    /// </summary>
    [Fact]
    public async Task ConsultarProcesso_LeDataCompactaEIsoNaMesmaResposta()
    {
        var (client, _, _) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, RespostaEncontrado));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        result.Value!.DataAjuizamento.Should().Be(new DateTimeOffset(2018, 10, 29, 0, 0, 0, TimeSpan.Zero));
        result.Value.Movimentos.Should().HaveCount(2);
    }

    /// <summary>Do mais recente para o mais antigo — é a ordem em que a timeline é lida na tela.</summary>
    [Fact]
    public async Task ConsultarProcesso_OrdenaMovimentosDoMaisRecenteParaOMaisAntigo()
    {
        var (client, _, _) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, RespostaEncontrado));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        result.Value!.Movimentos[0].Nome.Should().Be("Sentença");
        result.Value.Movimentos[1].Nome.Should().Be("Distribuição");
    }

    [Fact]
    public async Task ConsultarProcesso_QuandoNaoEncontrado_RetornaSucessoComNulo()
    {
        var (client, _, _) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, RespostaVazia));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        // Não é falha: o processo pode simplesmente não ter sido enviado pelo tribunal, e o
        // cadastro manual continua disponível.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>
    /// "Não existe" precisa entrar no cache. Sem isso, o número que o usuário mais reconsulta
    /// — justamente o que não foi achado — é o que mais bate no CNJ.
    /// </summary>
    [Fact]
    public async Task ConsultarProcesso_CacheiaTambemONaoEncontrado()
    {
        var (client, handler, cache) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, RespostaVazia));

        await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);
        var segunda = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        segunda.IsSuccess.Should().BeTrue();
        segunda.Value.Should().BeNull();
        handler.ReceivedUris.Should().HaveCount(1, "a segunda consulta deveria vir do cache");
        cache.Contains($"datajud:trf1:{NumeroProcesso}").Should().BeTrue();
    }

    [Fact]
    public async Task ConsultarProcesso_SegundaChamada_NaoTocaOCnj()
    {
        var (client, handler, _) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, RespostaEncontrado));

        await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);
        var segunda = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        segunda.Value!.Tribunal.Should().Be("TRF1");
        handler.ReceivedUris.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, nameof(ErrorCode.CNJ_AUTH_FAILED))]
    [InlineData(HttpStatusCode.Forbidden, nameof(ErrorCode.CNJ_AUTH_FAILED))]
    [InlineData(HttpStatusCode.TooManyRequests, nameof(ErrorCode.CNJ_RATE_LIMITED))]
    [InlineData(HttpStatusCode.InternalServerError, nameof(ErrorCode.CNJ_UNAVAILABLE))]
    [InlineData(HttpStatusCode.GatewayTimeout, nameof(ErrorCode.CNJ_TIMEOUT))]
    public async Task ConsultarProcesso_MapeiaStatusHttpParaCodigoDoCatalogo(HttpStatusCode status, string esperado)
    {
        var (client, _, _) = CreateClient(h => h.RespondWithStatus(status));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(esperado);
    }

    [Fact]
    public async Task ConsultarProcesso_QuandoRespostaNaoEJson_RetornaInvalidResponse()
    {
        var (client, _, _) = CreateClient(h => h.RespondWithJson(HttpStatusCode.OK, "<html>manutenção</html>"));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_INVALID_RESPONSE));
    }

    [Fact]
    public async Task ConsultarProcesso_QuandoARedeFalha_RetornaUnavailableSemCachear()
    {
        var (client, _, cache) = CreateClient(h => h.Throw(new HttpRequestException("sem rota")));

        var result = await client.ConsultarProcessoAsync("TRF1", NumeroProcesso, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_UNAVAILABLE));
        // Falha não vira cache: senão uma indisponibilidade de segundos bloquearia a consulta
        // por uma hora inteira.
        cache.Writes.Should().Be(0);
    }

    [Fact]
    public async Task ConsultarProcesso_QuandoTribunalNaoEstaNoDataJud_NaoChamaARede()
    {
        var (client, handler, _) = CreateClient(_ => { });

        var result = await client.ConsultarProcessoAsync("STF", NumeroProcesso, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED));
        handler.ReceivedUris.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("0000832-35.2018.4.01.3202")]
    public async Task ConsultarProcesso_ComNumeroForaDoFormatoDe20Digitos_Rejeita(string numero)
    {
        var (client, handler, _) = CreateClient(_ => { });

        var result = await client.ConsultarProcessoAsync("TRF1", numero, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.CNJ_INVALID_REQUEST));
        handler.ReceivedUris.Should().BeEmpty();
    }
}

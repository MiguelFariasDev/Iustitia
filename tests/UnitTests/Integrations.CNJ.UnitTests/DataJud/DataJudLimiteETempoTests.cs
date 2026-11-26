using System.Net;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;
using Advocacia.Modules.Integrations.CNJ.UnitTests.Djen;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.DataJud;

/// <summary>
/// Consulta de processo com o DataJud lento ou no limite: a pessoa espera diante do formulário,
/// então a resposta tem de vir rápida e legível, nunca um 500 depois de 30 s.
/// </summary>
public class DataJudLimiteETempoTests
{
    private const string Numero1 = "00008323520184013202";
    private const string Numero2 = "00008323520184013203";

    private static (DataJudClient Client, StubHttpMessageHandler Handler) CreateClient(Action<StubHttpMessageHandler> arrange)
    {
        var handler = new StubHttpMessageHandler();
        arrange(handler);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-publica.datajud.cnj.jus.br") };
        var client = new DataJudClient(
            httpClient, new FakeCacheService(), new DataJudTelemetry(), Options.Create(new DataJudOptions()),
            NullLogger<DataJudClient>.Instance);

        return (client, handler);
    }

    [Fact]
    public async Task DepoisDeUm429_AsConsultasSeguintesRespondemNaHora_SemChamarODataJud()
    {
        var (client, handler) = CreateClient(h => h.RespondWithStatus(HttpStatusCode.TooManyRequests));

        var primeira = await client.ConsultarProcessoAsync("TRF1", Numero1, CancellationToken.None);
        var segunda = await client.ConsultarProcessoAsync("TRF1", Numero2, CancellationToken.None);

        primeira.Error.Code.Should().Be(nameof(ErrorCode.CNJ_RATE_LIMITED));
        segunda.Error.Code.Should().Be(nameof(ErrorCode.CNJ_RATE_LIMITED));
        handler.ReceivedUris.Should().HaveCount(1, "a pausa evita gastar a cota pública de novo");
    }

    [Fact]
    public async Task TempoEsgotadoNoPipeline_ViraCnjTimeout_EmVezDeExcecao()
    {
        var (client, _) = CreateClient(h => h.Throw(new TimeoutRejectedException()));

        var resultado = await client.ConsultarProcessoAsync("TRF1", Numero1, CancellationToken.None);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_TIMEOUT));
    }

    [Fact]
    public async Task CircuitoAberto_ViraCnjUnavailable_EmVezDeExcecao()
    {
        var (client, _) = CreateClient(h => h.Throw(new BrokenCircuitException()));

        var resultado = await client.ConsultarProcessoAsync("TRF1", Numero1, CancellationToken.None);

        resultado.Error.Code.Should().Be(nameof(ErrorCode.CNJ_UNAVAILABLE));
    }

    [Fact]
    public async Task Pipeline_NaoRepete429_MasRepeteFalhaDoServidor()
    {
        var limite = new StubHttpMessageHandler()
            .RespondWithStatus(HttpStatusCode.TooManyRequests)
            .RespondWithStatus(HttpStatusCode.TooManyRequests);
        var instavel = new StubHttpMessageHandler()
            .RespondWithStatus(HttpStatusCode.ServiceUnavailable)
            .RespondWithStatus(HttpStatusCode.OK);

        (await Enviar(limite)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limite.ReceivedUris.Should().HaveCount(1);

        (await Enviar(instavel)).StatusCode.Should().Be(HttpStatusCode.OK);
        instavel.ReceivedUris.Should().HaveCount(2);
    }

    [Fact]
    public void PadroesCabemNaPacienciaDoNavegador()
    {
        var opcoes = new DataJudOptions();

        (opcoes.TimeoutSeconds * (opcoes.RetryCount + 1) + opcoes.RetryBaseDelaySeconds * opcoes.RetryCount)
            .Should().BeLessThan(30, "o navegador desiste aos 30 s");
    }

    private static async Task<HttpResponseMessage> Enviar(StubHttpMessageHandler stub)
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("datajud-teste", c => c.BaseAddress = new Uri("https://exemplo.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        DataJudResiliencePipeline.Configure(
            builder.AddStandardResilienceHandler(), new DataJudOptions { RetryBaseDelaySeconds = 0 });
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("datajud-teste");

        return await client.GetAsync("/x");
    }
}

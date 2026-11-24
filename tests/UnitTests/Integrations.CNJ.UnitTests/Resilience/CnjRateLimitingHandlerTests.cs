using System.Diagnostics;
using System.Net;
using Advocacia.Modules.Integrations.CNJ.Infrastructure;
using Advocacia.Modules.Integrations.CNJ.Infrastructure.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.Resilience;

/// <summary>
/// Throttling local para respeitar o limite medido do DJEN (20 req/janela de ~5s —
/// ver docs/poc/cnj-poc-report.md).
/// </summary>
public class CnjRateLimitingHandlerTests
{
    /// <summary>Conta requisições e responde 200 imediatamente — o alvo do limitador.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        internal int Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (HttpClient Client, CountingHandler Inner) Build(CnjOptions options)
    {
        var inner = new CountingHandler();
        var limiter = new CnjRateLimitingHandler(
            new CnjRateLimiter(Options.Create(options)), NullLogger<CnjRateLimitingHandler>.Instance)
        {
            InnerHandler = inner,
        };

        return (new HttpClient(limiter) { BaseAddress = new Uri("https://djen.invalid/") }, inner);
    }

    [Fact]
    public async Task AllowsUpToThePermitLimitWithoutDelay()
    {
        var (client, inner) = Build(new CnjOptions { RateLimitPermitsPerWindow = 5, RateLimitWindowSeconds = 5 });

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++)
        {
            await client.GetAsync("x", CancellationToken.None);
        }

        stopwatch.Stop();

        inner.Count.Should().Be(5);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "as 5 primeiras cabem na janela e não devem esperar");
    }

    [Fact]
    public async Task ThrottlesBeyondThePermitLimitInsteadOfLettingTheRequestThrough()
    {
        // A requisição que excede a janela ESPERA em vez de ir para a rede e tomar 429 —
        // que é o ponto: 429 consumiria uma tentativa de retry (ver ADR-059).
        var (client, inner) = Build(new CnjOptions
        {
            RateLimitPermitsPerWindow = 2,
            RateLimitWindowSeconds = 2,
            RateLimitQueueTimeoutSeconds = 30,
        });

        for (var i = 0; i < 2; i++)
        {
            await client.GetAsync("x", CancellationToken.None);
        }

        inner.Count.Should().Be(2);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("x", CancellationToken.None);
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Count.Should().Be(3);
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(500), "a 3ª requisição deveria ter aguardado a janela liberar");
    }

    [Fact]
    public async Task WhenTheQueueTimeoutExpires_ReturnsTooManyRequestsInsteadOfThrowing()
    {
        // Devolver 429 (e não exceção) faz o pipeline de resiliência tratar como transiente,
        // que é exatamente o que é.
        var (client, inner) = Build(new CnjOptions
        {
            RateLimitPermitsPerWindow = 1,
            RateLimitWindowSeconds = 60,
            RateLimitQueueTimeoutSeconds = 1,
        });

        await client.GetAsync("x", CancellationToken.None);

        var response = await client.GetAsync("x", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        inner.Count.Should().Be(1, "a 2ª requisição nunca deveria ter chegado à rede");
    }

    [Fact]
    public async Task CallerCancellation_IsPropagatedAndNotSwallowedAsRateLimit()
    {
        var (client, _) = Build(new CnjOptions
        {
            RateLimitPermitsPerWindow = 1,
            RateLimitWindowSeconds = 60,
            RateLimitQueueTimeoutSeconds = 60,
        });

        await client.GetAsync("x", CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = () => client.GetAsync("x", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

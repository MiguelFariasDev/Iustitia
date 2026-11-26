using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace Advocacia.BuildingBlocks.Infrastructure.Resilience;

/// <summary>Parâmetros do pipeline HTTP de um cliente externo (CNJ, DataJud, Anthropic...).</summary>
/// <param name="TimeoutSeconds">Timeout de CADA tentativa; o total é este valor × (<paramref name="MaxRetries"/> + 1).</param>
/// <param name="MaxRetries">Repetições além da primeira tentativa.</param>
/// <param name="TreatOverloadedAsTransient">529 (overloaded, da Anthropic) é transitório.</param>
/// <param name="RetryOnTooManyRequests">
/// false = 429 não é REPETIDO na hora (continua contando para o circuito): repetir um "limite excedido" só gasta
/// a cota e o tempo de quem espera.
/// </param>
public sealed record StandardHttpResilienceProfile(
    int TimeoutSeconds,
    int MaxRetries,
    int RetryBaseDelaySeconds,
    double CircuitBreakerFailureRatio,
    int CircuitBreakerSamplingDurationSeconds,
    int CircuitBreakerMinimumThroughput,
    int CircuitBreakerBreakDurationSeconds,
    bool TreatOverloadedAsTransient = false,
    bool RetryOnTooManyRequests = true);

/// <summary>
/// Pipeline de resiliência compartilhado pelos clientes HTTP externos (Polly 8 — ADR-067). A ordem importa:
/// timeout total (mais externo) &gt; circuit breaker &gt; retry &gt; timeout por tentativa. Retry com backoff
/// exponencial e jitter, para não sincronizar rajadas entre clientes. 4xx (exceto 408/429) NÃO é repetido:
/// é erro de requisição nosso e repetir só gasta quota — ou dinheiro, no caso da IA.
/// </summary>
public static class StandardHttpResilience
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, StandardHttpResilienceProfile profile)
    {
        builder.Configure(resilience =>
        {
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(profile.TimeoutSeconds * (profile.MaxRetries + 1));
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(profile.TimeoutSeconds);

            resilience.Retry.MaxRetryAttempts = profile.MaxRetries;
            resilience.Retry.Delay = TimeSpan.FromSeconds(profile.RetryBaseDelaySeconds);
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = true;
            resilience.Retry.ShouldHandle = arguments => ValueTask.FromResult(
                IsTransient(arguments.Outcome, profile.TreatOverloadedAsTransient)
                && (profile.RetryOnTooManyRequests || arguments.Outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests));

            resilience.CircuitBreaker.FailureRatio = profile.CircuitBreakerFailureRatio;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(profile.CircuitBreakerSamplingDurationSeconds);
            resilience.CircuitBreaker.MinimumThroughput = profile.CircuitBreakerMinimumThroughput;
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(profile.CircuitBreakerBreakDurationSeconds);
            resilience.CircuitBreaker.ShouldHandle = arguments => ValueTask.FromResult(
                IsTransient(arguments.Outcome, profile.TreatOverloadedAsTransient));

            // O timeout de uma tentativa precisa caber na janela de amostragem do breaker, senão o Polly
            // rejeita a configuração na inicialização.
            if (resilience.CircuitBreaker.SamplingDuration < resilience.AttemptTimeout.Timeout * 2)
            {
                resilience.CircuitBreaker.SamplingDuration = resilience.AttemptTimeout.Timeout * 2;
            }
        });

        return builder;
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome, bool treatOverloadedAsTransient) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException
        || outcome.Result is { } response && IsTransient(response.StatusCode, treatOverloadedAsTransient);

    private static bool IsTransient(HttpStatusCode statusCode, bool treatOverloadedAsTransient) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
        || treatOverloadedAsTransient && (int)statusCode == 529;
}

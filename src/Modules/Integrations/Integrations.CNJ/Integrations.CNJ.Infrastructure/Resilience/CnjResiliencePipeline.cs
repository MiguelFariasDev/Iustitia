using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.Resilience;

/// <summary>
/// Pipeline de resiliência do cliente do DJEN (Polly 8 — ver claude.md, seção 2). A ordem
/// importa: timeout total (mais externo) &gt; circuit breaker &gt; retry &gt; timeout por
/// tentativa. O CNJ é notoriamente instável em horário de pico, então o retry usa backoff
/// exponencial com jitter para não sincronizar rajadas de tentativas entre tribunais.
///
/// 4xx (exceto 408/429) NÃO são retentados: são erros de requisição nossos e repetir só
/// gasta quota. 429 é retentado respeitando o backoff.
/// </summary>
public static class CnjResiliencePipeline
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, CnjOptions options)
    {
        builder.Configure(resilience =>
        {
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds * (options.RetryCount + 1));
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            resilience.Retry.MaxRetryAttempts = options.RetryCount;
            resilience.Retry.Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds);
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = true;
            resilience.Retry.ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));

            resilience.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds);
            resilience.CircuitBreaker.MinimumThroughput = options.CircuitBreakerMinimumThroughput;
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds);
            resilience.CircuitBreaker.ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));

            // AttemptTimeout precisa caber na janela de amostragem do breaker, senão o
            // Polly rejeita a configuração na inicialização.
            if (resilience.CircuitBreaker.SamplingDuration < resilience.AttemptTimeout.Timeout * 2)
            {
                resilience.CircuitBreaker.SamplingDuration = resilience.AttemptTimeout.Timeout * 2;
            }
        });

        return builder;
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException
        || outcome.Result is { } response && IsTransient(response.StatusCode);

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}

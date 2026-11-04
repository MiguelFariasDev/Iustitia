using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Resilience;

/// <summary>
/// Mesma ordem do pipeline do CNJ (ver ADR-067): timeout total &gt; circuit breaker &gt; retry &gt; timeout
/// por tentativa. 4xx (exceto 408/429) não é retentado — é erro nosso e repetir só gasta
/// dinheiro. 529 (overloaded) é tratado como transitório.
/// </summary>
public static class AnthropicResiliencePipeline
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, AnthropicSettings options)
    {
        builder.Configure(resilience =>
        {
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds * (options.MaxRetries + 1));
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            resilience.Retry.MaxRetryAttempts = options.MaxRetries;
            resilience.Retry.Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds);
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = true;
            resilience.Retry.ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));

            resilience.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds);
            resilience.CircuitBreaker.MinimumThroughput = options.CircuitBreakerMinimumThroughput;
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds);
            resilience.CircuitBreaker.ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));

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
            or HttpStatusCode.GatewayTimeout
            or (HttpStatusCode)529;
}

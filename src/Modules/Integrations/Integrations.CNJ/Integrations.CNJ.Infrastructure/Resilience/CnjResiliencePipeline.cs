using Advocacia.BuildingBlocks.Infrastructure.Resilience;
using Microsoft.Extensions.Http.Resilience;

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.Resilience;

/// <summary>
/// Pipeline de resiliência do cliente do DJEN: o padrão compartilhado (<see cref="StandardHttpResilience"/>).
/// O CNJ é notoriamente instável em horário de pico, por isso o retry usa backoff exponencial com jitter; 429 é
/// repetido respeitando o backoff.
/// </summary>
public static class CnjResiliencePipeline
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, CnjOptions options) =>
        StandardHttpResilience.Configure(builder, new StandardHttpResilienceProfile(
            options.TimeoutSeconds,
            options.RetryCount,
            options.RetryBaseDelaySeconds,
            options.CircuitBreakerFailureRatio,
            options.CircuitBreakerSamplingDurationSeconds,
            options.CircuitBreakerMinimumThroughput,
            options.CircuitBreakerBreakDurationSeconds));
}

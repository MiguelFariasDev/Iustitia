using Advocacia.BuildingBlocks.Infrastructure.Resilience;
using Microsoft.Extensions.Http.Resilience;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Resilience;

/// <summary>
/// Pipeline do cliente da Anthropic: o padrão compartilhado (<see cref="StandardHttpResilience"/>, ADR-067) com
/// 529 (overloaded) tratado como transitório.
/// </summary>
public static class AnthropicResiliencePipeline
{
    public static IHttpStandardResiliencePipelineBuilder Configure(
        IHttpStandardResiliencePipelineBuilder builder, AnthropicSettings options) =>
        StandardHttpResilience.Configure(builder, new StandardHttpResilienceProfile(
            options.TimeoutSeconds,
            options.MaxRetries,
            options.RetryBaseDelaySeconds,
            options.CircuitBreakerFailureRatio,
            options.CircuitBreakerSamplingDurationSeconds,
            options.CircuitBreakerMinimumThroughput,
            options.CircuitBreakerBreakDurationSeconds,
            TreatOverloadedAsTransient: true));
}

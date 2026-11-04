using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure;

public sealed class AiCostCalculator(IOptions<AnthropicOptions> options, ILogger<AiCostCalculator> logger)
    : IAiCostCalculator
{
    private const decimal TokensPerMillion = 1_000_000m;

    public decimal Calculate(string model, AnthropicUsage usage)
    {
        if (!options.Value.Pricing.TryGetValue(model, out var pricing))
        {
            logger.LogWarning("Anthropic: sem preço configurado para o modelo {Model}; custo registrado como 0.", model);
            return 0m;
        }

        return (usage.InputTokens * pricing.InputPerMillion
                + usage.OutputTokens * pricing.OutputPerMillion
                + usage.CacheCreationInputTokens * pricing.CacheWritePerMillion
                + usage.CacheReadInputTokens * pricing.CacheReadPerMillion)
            / TokensPerMillion;
    }
}

using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Integrations.Anthropic.UnitTests;

public class AiCostCalculatorTests
{
    private static AiCostCalculator Create() => new(
        Options.Create(new AnthropicOptions
        {
            Pricing =
            {
                ["modelo-teste"] = new ModelPricing
                {
                    InputPerMillion = 2m,
                    OutputPerMillion = 10m,
                    CacheWritePerMillion = 2.5m,
                    CacheReadPerMillion = 0.2m,
                },
            },
        }),
        NullLogger<AiCostCalculator>.Instance);

    [Fact]
    public void Calculate_AppliesEachTariffToItsTokenKind()
    {
        // 1M entrada = 2; 1M saída = 10; 1M escrita de cache = 2,5; 1M leitura de cache = 0,2.
        var cost = Create().Calculate("modelo-teste", new AnthropicUsage(1_000_000, 1_000_000, 1_000_000, 1_000_000));

        cost.Should().Be(14.7m);
    }

    [Fact]
    public void Calculate_SmallCall_KeepsSubCentPrecision()
    {
        var cost = Create().Calculate("modelo-teste", new AnthropicUsage(2_000, 150, 0, 0));

        cost.Should().Be(0.0055m);
    }

    [Fact]
    public void Calculate_UnknownModel_ReturnsZeroInsteadOfFailing()
    {
        Create().Calculate("modelo-sem-preco", new AnthropicUsage(100, 100, 0, 0)).Should().Be(0m);
    }
}

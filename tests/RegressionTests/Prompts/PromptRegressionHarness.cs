using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Infrastructure.Services;
using Advocacia.Modules.Workflow.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.RegressionTests.Prompts;

/// <summary>
/// Monta os serviços REAIS de classificação e prazo contra a API REAL da Anthropic, sem
/// container de DI nem banco: o que se testa aqui é o PROMPT (e o modelo configurado), não a
/// infraestrutura.
///
/// Estes testes custam dinheiro (≈ 20 chamadas por execução) e por isso:
///   - têm a trait Category=PromptRegression e NÃO devem rodar em CI;
///   - são pulados quando ANTHROPIC_API_KEY não está definida.
/// Rodar manualmente, ao mudar um prompt (e subir a versão no .md) ou o modelo:
///   ANTHROPIC_API_KEY=... dotnet test --filter Category=PromptRegression
/// Para testar outro modelo: ANTHROPIC_MODEL=... (padrão: claude-sonnet-5-5).
/// </summary>
internal static class PromptRegressionHarness
{
    public const string Category = "PromptRegression";
    public const string ApiKeyVariable = "ANTHROPIC_API_KEY";

    private static readonly Lazy<(IClassificationService Classification, IDeadlineSuggestionService Deadline)> Services =
        new(Build);

    public static bool HasApiKey => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyVariable));

    public static IClassificationService Classification => Services.Value.Classification;

    public static IDeadlineSuggestionService Deadline => Services.Value.Deadline;

    private static (IClassificationService, IDeadlineSuggestionService) Build()
    {
        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") is { Length: > 0 } configured
            ? configured
            : "claude-sonnet-5-5";

        var anthropicOptions = new AnthropicOptions
        {
            ApiKey = Environment.GetEnvironmentVariable(ApiKeyVariable) ?? string.Empty,
        };

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(anthropicOptions.BaseUrl),
            Timeout = TimeSpan.FromSeconds(180),
        };

        IAnthropicClient client = new AnthropicClient(
            httpClient, Options.Create(anthropicOptions), NullLogger<AnthropicClient>.Instance);

        var models = Options.Create(new AnthropicModelsOptions { Classification = model, Deadline = model });

        var executor = new AiPromptExecutor(
            new EmbeddedPromptCatalog(),
            client,
            new ZeroCostCalculator(),
            new NullUsageRecorder(),
            models,
            NullLogger<AiPromptExecutor>.Instance);

        return (
            new ClassificationService(executor, models, NullLogger<ClassificationService>.Instance),
            new DeadlineSuggestionService(executor, models, NullLogger<DeadlineSuggestionService>.Instance));
    }

    private sealed class ZeroCostCalculator : IAiCostCalculator
    {
        public decimal Calculate(string model, AnthropicUsage usage) => 0m;
    }

    private sealed class NullUsageRecorder : IAiUsageRecorder
    {
        public Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

/// <summary>[Theory] que se pula sozinho quando não há chave de API.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class PromptTheoryAttribute : TheoryAttribute
{
    public PromptTheoryAttribute()
    {
        if (!PromptRegressionHarness.HasApiKey)
        {
            Skip = $"{PromptRegressionHarness.ApiKeyVariable} não definida — teste de regressão de prompt pulado (chama a API real).";
        }
    }
}

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure;

/// <summary>
/// Seção "Anthropic" do appsettings. ApiKey NUNCA vai em arquivo versionado: user-secrets em
/// dev, Azure Key Vault em produção (ver docs/compliance/production-config.md).
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string BaseUrl { get; init; } = "https://api.anthropic.com/";

    public string ApiKey { get; init; } = string.Empty;

    public string ApiVersion { get; init; } = "2023-06-01";

    /// <summary>
    /// Esforço de raciocínio (output_config.effort): low, medium, high, xhigh ou max. Vazio
    /// omite o campo e deixa o padrão do modelo. Classificação não precisa de esforço alto.
    /// </summary>
    public string Effort { get; init; } = "medium";

    public int TimeoutSeconds { get; init; } = 60;

    public int RetryCount { get; init; } = 3;

    public int RetryBaseDelaySeconds { get; init; } = 2;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 120;

    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    public int CircuitBreakerBreakDurationSeconds { get; init; } = 60;

    /// <summary>Preço por modelo (USD por milhão de tokens). Chave = ID exato do modelo.</summary>
    public Dictionary<string, ModelPricing> Pricing { get; init; } = [];
}

/// <summary>
/// Tarifas em USD por milhão de tokens. Conferir na página de preços da Anthropic ao trocar
/// de modelo: valor desatualizado só distorce o custo registrado, nunca o comportamento.
/// </summary>
public sealed class ModelPricing
{
    public decimal InputPerMillion { get; init; }

    public decimal OutputPerMillion { get; init; }

    public decimal CacheWritePerMillion { get; init; }

    public decimal CacheReadPerMillion { get; init; }
}

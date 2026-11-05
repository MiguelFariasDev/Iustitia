namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure;

/// <summary>
/// Seção "Anthropic" do appsettings. ApiKey NUNCA vai em arquivo versionado: user-secrets em
/// dev, Azure Key Vault (<c>Anthropic--ApiKey</c>) em produção.
/// </summary>
public sealed class AnthropicSettings
{
    public const string SectionName = "Anthropic";

    /// <summary>Base da API, com o <c>/v1/</c> (a rota relativa do cliente é <c>messages</c>).</summary>
    public string BaseUrl { get; init; } = "https://api.anthropic.com/v1/";

    public string ApiKey { get; init; } = string.Empty;

    public string ApiVersion { get; init; } = "2023-06-01";

    /// <summary>Modelo usado quando a chamada não informa um.</summary>
    public string DefaultModel { get; init; } = "claude-sonnet-5-5";

    /// <summary>Teto de tokens de saída quando a chamada não informa um.</summary>
    public int DefaultMaxTokens { get; init; } = 4096;

    /// <summary>
    /// Esforço de raciocínio (output_config.effort): low, medium, high, xhigh ou max. Vazio
    /// omite o campo e deixa o padrão do modelo.
    /// </summary>
    public string Effort { get; init; } = "medium";

    /// <summary>Timeout de CADA tentativa. O total é este valor × (MaxRetries + 1).</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Tentativas extras em 429/5xx/529/timeout, com backoff exponencial (2s, 4s, 8s) e jitter.</summary>
    public int MaxRetries { get; init; } = 3;

    public int RetryBaseDelaySeconds { get; init; } = 2;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    /// <summary>
    /// Janela do circuit breaker. O Polly exige janela ≥ 2× o timeout por tentativa, então com
    /// TimeoutSeconds=60 o mínimo é 120 — o "5 falhas em 30s" do desenho original não é
    /// configurável com timeout de 60s (ver ADR-067).
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 120;

    public int CircuitBreakerMinimumThroughput { get; init; } = 5;

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

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure.DataJud;

/// <summary>
/// Seção "DataJud" do appsettings — endpoint, credencial e políticas da API Pública do
/// DataJud (CNJ).
///
/// A <see cref="ApiKey"/> é PÚBLICA: o CNJ a divulga na wiki do DataJud e ela é a mesma
/// para todo mundo. Ainda assim fica fora do appsettings versionado (user-secrets em dev,
/// Key Vault em produção), por duas razões: o CNJ pode trocá-la sem aviso, e credencial
/// commitada vira hábito.
/// </summary>
public sealed class DataJudOptions
{
    public const string SectionName = "DataJud";

    public string BaseUrl { get; init; } = "https://api-publica.datajud.cnj.jus.br";

    /// <summary>Valor do header Authorization, sem o prefixo "APIKey".</summary>
    public string ApiKey { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public int RetryCount { get; init; } = 3;

    public int RetryBaseDelaySeconds { get; init; } = 2;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 30;

    public int CircuitBreakerMinimumThroughput { get; init; } = 5;

    public int CircuitBreakerBreakDurationSeconds { get; init; } = 60;

    /// <summary>
    /// Quanto tempo a resposta fica em cache. Uma hora porque metadado de processo muda em
    /// escala de dias, e a regra do CNJ é não fazer consulta em massa — cache é a forma
    /// mais direta de respeitá-la.
    /// </summary>
    public int CacheTtlMinutes { get; init; } = 60;
}

namespace Advocacia.Modules.Integrations.CNJ.Infrastructure;

/// <summary>Seção "Cnj" do appsettings — endpoint e políticas de resiliência do DJEN.</summary>
public sealed class CnjOptions
{
    public const string SectionName = "Cnj";

    /// <summary>Base da API pública de comunicações do CNJ (DJEN). Não exige autenticação.</summary>
    public string BaseUrl { get; init; } = "https://comunicaapi.pje.jus.br/";

    /// <summary>Caminho relativo do recurso de consulta de comunicações.</summary>
    public string PublicationsPath { get; init; } = "api/v1/comunicacao";

    /// <summary>Máximo de itens por página aceito pela API — consultas maiores são recortadas pelo cliente.</summary>
    public int MaxPageSize { get; init; } = 100;

    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Tentativas de retry por requisição (além da original), com backoff exponencial e jitter.</summary>
    public int RetryCount { get; init; } = 3;

    public int RetryBaseDelaySeconds { get; init; } = 2;

    /// <summary>Proporção de falhas na janela de amostragem que abre o circuito (0.0–1.0).</summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 30;

    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    public int CircuitBreakerBreakDurationSeconds { get; init; } = 60;

    /// <summary>
    /// Requisições permitidas por janela deslizante (throttling do lado do cliente).
    ///
    /// Medido na prova de conceito (Etapa 1.3, ver docs/poc/cnj-poc-metrics.md): o DJEN
    /// aceita 20 requisições e devolve 429 na 21ª, recuperando em até ~5s — resultado
    /// reprodutível em três execuções independentes. O padrão de 18 deixa margem para o
    /// relógio do servidor não coincidir com o nosso e para outra instância do Worker
    /// dividir o mesmo orçamento.
    ///
    /// Sem isso, uma execução de produção (MaxPages=50 × 4 tribunais = 200 requisições)
    /// levaria 429 a partir da 21ª e passaria o resto da execução em retry.
    /// </summary>
    public int RateLimitPermitsPerWindow { get; init; } = 18;

    public int RateLimitWindowSeconds { get; init; } = 5;

    /// <summary>
    /// Quanto uma requisição espera na fila do rate limiter antes de desistir. Generoso de
    /// propósito: esperar 2 minutos é muito melhor que tomar 429 e queimar uma tentativa de
    /// retry — a captura é um job de fundo, latência não incomoda ninguém.
    /// </summary>
    public int RateLimitQueueTimeoutSeconds { get; init; } = 120;
}

using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Legal.Domain.Ai;

/// <summary>
/// Registro de auditoria de UMA chamada ao provedor de IA (claude.md, seção 16: logar todas as
/// chamadas — tokens, custo). Append-only e SEM conteúdo: nem o texto da publicação nem a
/// resposta do modelo entram aqui, só metadados. O vínculo com o dado é o
/// <see cref="ReferenceId"/> (a publicação).
///
/// Chamada que falha na validação continua gerando registro: os tokens foram consumidos e
/// cobrados mesmo que a resposta tenha sido descartada.
/// </summary>
public sealed class AiUsageLog : Entity<AiUsageLogId>, IHasTenant
{
    public Guid TenantId { get; private set; }

    /// <summary>classification ou deadline.</summary>
    public string Purpose { get; private set; } = null!;

    public string Provider { get; private set; } = null!;

    public string Model { get; private set; } = null!;

    public string PromptVersion { get; private set; } = null!;

    /// <summary>Publicação à qual a chamada se refere, quando houver.</summary>
    public Guid? ReferenceId { get; private set; }

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public int CacheCreationInputTokens { get; private set; }

    public int CacheReadInputTokens { get; private set; }

    /// <summary>Custo em USD pela tabela de preços configurada no momento da chamada.</summary>
    public decimal CostUsd { get; private set; }

    public int LatencyMs { get; private set; }

    public AiCallStatus Status { get; private set; }

    /// <summary>Código do catálogo de erros quando <see cref="Status"/> não é Succeeded.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>request-id da Anthropic, para abrir chamado de suporte sobre uma chamada específica.</summary>
    public string? RequestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AiUsageLog()
    {
        // EF Core.
    }

    private AiUsageLog(AiUsageLogId id)
        : base(id)
    {
    }

    public static AiUsageLog Create(
        Guid tenantId,
        string purpose,
        string provider,
        string model,
        string promptVersion,
        Guid? referenceId,
        int inputTokens,
        int outputTokens,
        int cacheCreationInputTokens,
        int cacheReadInputTokens,
        decimal costUsd,
        int latencyMs,
        AiCallStatus status,
        string? errorCode,
        string? requestId,
        DateTimeOffset createdAt) =>
        new(AiUsageLogId.New())
        {
            TenantId = tenantId,
            Purpose = purpose,
            Provider = provider,
            Model = model,
            PromptVersion = promptVersion,
            ReferenceId = referenceId,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CostUsd = costUsd,
            LatencyMs = latencyMs,
            Status = status,
            ErrorCode = errorCode,
            RequestId = requestId,
            CreatedAt = createdAt.ToUniversalTime(),
        };
}

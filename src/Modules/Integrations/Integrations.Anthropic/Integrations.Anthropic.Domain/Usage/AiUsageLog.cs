using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Integrations.Anthropic.Domain.Usage;

/// <summary>
/// Registro de auditoria de UMA chamada à IA (claude.md, seção 16): quem, qual modelo, quantos
/// tokens, quanto custou, quanto demorou, se deu certo. Append-only.
///
/// NUNCA tem o texto do prompt nem da resposta (sigilo profissional — ADR-069). O vínculo com o
/// dado de negócio é só o <see cref="ReferenceId"/> (ex.: a publicação).
///
/// Uma resposta que chegou mas foi descartada pela validação do consumidor também é registrada
/// (<see cref="Success"/> falso, <see cref="ErrorCode"/> AI_INVALID_RESPONSE): os tokens foram
/// consumidos e cobrados.
/// </summary>
public sealed class AiUsageLog : Entity<AiUsageLogId>, IHasTenant
{
    public Guid TenantId { get; private set; }

    /// <summary>Usuário que originou a chamada; nulo em execuções de job.</summary>
    public Guid? UserId { get; private set; }

    public string Model { get; private set; } = null!;

    /// <summary>Finalidade da chamada (ex.: classification, deadline); nulo quando genérica.</summary>
    public string? Purpose { get; private set; }

    public string? PromptVersion { get; private set; }

    /// <summary>Id do objeto de negócio ao qual a chamada se refere (ex.: a publicação).</summary>
    public Guid? ReferenceId { get; private set; }

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public int CacheCreationInputTokens { get; private set; }

    public int CacheReadInputTokens { get; private set; }

    /// <summary>Custo em USD pela tabela de preços configurada no momento da chamada.</summary>
    public decimal CostUsd { get; private set; }

    public int LatencyMs { get; private set; }

    public bool Success { get; private set; }

    /// <summary>Código do catálogo de erros quando <see cref="Success"/> é falso.</summary>
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
        Guid? userId,
        string model,
        string? purpose,
        string? promptVersion,
        Guid? referenceId,
        int inputTokens,
        int outputTokens,
        int cacheCreationInputTokens,
        int cacheReadInputTokens,
        decimal costUsd,
        int latencyMs,
        bool success,
        string? errorCode,
        string? requestId,
        DateTimeOffset createdAt) =>
        new(AiUsageLogId.New())
        {
            TenantId = tenantId,
            UserId = userId,
            Model = model,
            Purpose = purpose,
            PromptVersion = promptVersion,
            ReferenceId = referenceId,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CostUsd = costUsd,
            LatencyMs = latencyMs,
            Success = success,
            ErrorCode = errorCode,
            RequestId = requestId,
            CreatedAt = createdAt.ToUniversalTime(),
        };
}

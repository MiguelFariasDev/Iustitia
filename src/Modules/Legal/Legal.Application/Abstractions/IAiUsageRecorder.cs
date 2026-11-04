using Advocacia.Modules.Legal.Domain.Ai;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Grava um registro em ai_usage_logs para CADA chamada ao provedor de IA — inclusive as que
/// falham na validação, que consomem tokens do mesmo jeito. Tenant, publicação e horário vêm
/// do contexto da execução, não de quem chama.
/// </summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>Fatos de uma chamada. Sem texto de prompt nem de resposta, de propósito.</summary>
public sealed record AiUsageEntry(
    string Purpose,
    string Provider,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens,
    int CacheReadInputTokens,
    decimal CostUsd,
    int LatencyMs,
    AiCallStatus Status,
    string? ErrorCode,
    string? RequestId);

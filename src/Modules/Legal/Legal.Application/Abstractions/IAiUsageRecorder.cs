namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Registra em ai_usage_logs CADA chamada à IA feita pelo módulo Legal — inclusive as que
/// falham na validação, que consomem tokens do mesmo jeito. Tenant, usuário e publicação vêm
/// do contexto da execução, não de quem chama. A tabela é do módulo Integrations.Anthropic
/// (<c>IAnthropicUsageLogger</c>); este adaptador só traduz o contexto do Legal.
/// </summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>Fatos de uma chamada. Sem texto de prompt nem de resposta, de propósito.</summary>
/// <param name="Success">Falso quando a chamada falhou OU a resposta foi descartada pela validação.</param>
/// <param name="ErrorCode">Código do catálogo de erros quando <paramref name="Success"/> é falso.</param>
public sealed record AiUsageEntry(
    string Purpose,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens,
    int CacheReadInputTokens,
    long LatencyMs,
    bool Success,
    string? ErrorCode,
    string? RequestId);

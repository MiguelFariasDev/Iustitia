using Advocacia.Modules.Integrations.Anthropic.Domain.Models;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Models;

/// <summary>
/// Fatos de uma chamada para auditoria. SEM texto de prompt nem de resposta, de propósito.
/// O custo não vem aqui: é calculado pelo logger a partir dos tokens e da tabela de preços.
/// </summary>
public sealed record AnthropicUsageLogEntry(
    Guid TenantId,
    Guid? UserId,
    string Model,
    AnthropicUsage Usage,
    long LatencyMs,
    bool Success,
    string? ErrorCode = null,
    string? Purpose = null,
    string? PromptVersion = null,
    Guid? ReferenceId = null,
    string? RequestId = null);

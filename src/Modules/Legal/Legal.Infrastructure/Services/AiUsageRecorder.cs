using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Legal.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Infrastructure.Services;

/// <summary>
/// Adaptador do módulo Legal sobre <see cref="IAnthropicUsageLogger"/> (dono da tabela
/// ai_usage_logs — ADR-069): acrescenta o que só o Legal sabe — o tenant da execução, o usuário
/// (nulo em job) e a publicação em processamento.
///
/// Falha ao gravar a auditoria PROPAGA, de propósito: uma chamada paga sem registro é pior que
/// uma execução do job que falha e é retentada.
/// </summary>
public sealed class AiUsageRecorder(
    IAnthropicUsageLogger usageLogger,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IAiUsageContext usageContext,
    ILogger<AiUsageRecorder> logger) : IAiUsageRecorder
{
    public Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            // Sem tenant o RLS rejeitaria a escrita. Chamada de IA sem tenant é erro de
            // programação (o job sempre define o tenant antes) — melhor falhar alto.
            logger.LogError("IA: registro de uso sem tenant definido ({Purpose}) — não foi possível auditar.", entry.Purpose);
            throw new InvalidOperationException("Chamada de IA sem tenant definido: não é possível registrar o uso.");
        }

        return usageLogger.LogAsync(
            new AnthropicUsageLogEntry(
                tenantContext.TenantId,
                currentUser.UserId,
                entry.Model,
                new AnthropicUsage(
                    entry.InputTokens, entry.OutputTokens, entry.CacheCreationInputTokens, entry.CacheReadInputTokens),
                entry.LatencyMs,
                entry.Success,
                entry.ErrorCode,
                entry.Purpose,
                entry.PromptVersion,
                usageContext.PublicationId,
                entry.RequestId),
            cancellationToken);
    }
}

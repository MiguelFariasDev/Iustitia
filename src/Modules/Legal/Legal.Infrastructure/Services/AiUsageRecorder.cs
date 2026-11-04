using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Ai;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Infrastructure.Services;

/// <summary>
/// Persiste cada chamada de IA em ai_usage_logs, em um SaveChanges PRÓPRIO e imediato: a
/// auditoria do custo não pode depender de a sugestão ser gravada com sucesso depois (a
/// chamada foi feita e cobrada de qualquer jeito). Por isso o job grava a sugestão em outro
/// SaveChanges, e este não deve ser chamado com outras alterações pendentes no contexto.
///
/// Falha ao gravar a auditoria PROPAGA, de propósito: uma chamada paga sem registro é pior que
/// uma execução do job que falha e é retentada.
/// </summary>
public sealed class AiUsageRecorder(
    LegalDbContext dbContext,
    ITenantContext tenantContext,
    IAiUsageContext usageContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AiUsageRecorder> logger) : IAiUsageRecorder
{
    public async Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            // Sem tenant o RLS rejeitaria a escrita. Chamada de IA sem tenant é erro de
            // programação (o job sempre define o tenant antes) — melhor falhar alto.
            logger.LogError("IA: registro de uso sem tenant definido ({Purpose}) — não foi possível auditar.", entry.Purpose);
            throw new InvalidOperationException("Chamada de IA sem tenant definido: não é possível registrar o uso.");
        }

        dbContext.AiUsageLogs.Add(AiUsageLog.Create(
            tenantContext.TenantId,
            entry.Purpose,
            entry.Provider,
            entry.Model,
            entry.PromptVersion,
            usageContext.PublicationId,
            entry.InputTokens,
            entry.OutputTokens,
            entry.CacheCreationInputTokens,
            entry.CacheReadInputTokens,
            entry.CostUsd,
            entry.LatencyMs,
            entry.Status,
            entry.ErrorCode,
            entry.RequestId,
            dateTimeProvider.UtcNow));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Relançado como outro tipo de propósito: o job trata DbUpdateException como falha
            // de UMA publicação e segue; falha de AUDITORIA tem de parar a execução.
            dbContext.ChangeTracker.Clear();
            throw new InvalidOperationException("Não foi possível registrar o uso de IA em ai_usage_logs.", exception);
        }

        logger.LogInformation(
            "IA: uso registrado — {Purpose}, {Model}, {InputTokens} in / {OutputTokens} out, US$ {CostUsd}, {Status}.",
            entry.Purpose, entry.Model, entry.InputTokens, entry.OutputTokens, entry.CostUsd, entry.Status);
    }
}

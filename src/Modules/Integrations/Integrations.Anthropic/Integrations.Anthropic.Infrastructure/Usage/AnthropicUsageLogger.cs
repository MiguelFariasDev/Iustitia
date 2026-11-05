using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Usage;
using Advocacia.Modules.Integrations.Anthropic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Usage;

/// <summary>
/// Grava cada chamada em ai_usage_logs, num SaveChanges PRÓPRIO e imediato do
/// <see cref="AnthropicDbContext"/> — contexto separado do de quem chama, então não arrasta
/// alterações pendentes de outro módulo nem é desfeito por um rollback dele: a chamada foi
/// feita e cobrada de qualquer jeito.
///
/// O custo é calculado aqui, a partir dos tokens e da tabela de preços (Anthropic:Pricing).
///
/// NUNCA recebe nem grava texto de prompt/resposta: o tipo de entrada não tem onde carregá-lo.
/// O log de sistema carrega só contadores, modelo e custo.
/// </summary>
public sealed class AnthropicUsageLogger(
    AnthropicDbContext dbContext,
    IAiCostCalculator costCalculator,
    IDateTimeProvider dateTimeProvider,
    ILogger<AnthropicUsageLogger> logger) : IAnthropicUsageLogger
{
    public Task LogUsageAsync(
        Guid tenantId,
        Guid userId,
        string model,
        AnthropicUsage usage,
        long latencyMs,
        CancellationToken cancellationToken = default) =>
        LogAsync(
            new AnthropicUsageLogEntry(tenantId, userId == Guid.Empty ? null : userId, model, usage, latencyMs, Success: true),
            cancellationToken);

    public async Task LogAsync(AnthropicUsageLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.TenantId == Guid.Empty)
        {
            // Sem tenant o RLS rejeitaria a escrita. É erro de programação (job e requisição
            // sempre definem o tenant antes): melhor falhar alto que perder a auditoria.
            throw new ArgumentException("Registro de uso de IA exige o tenant.", nameof(entry));
        }

        var cost = costCalculator.Calculate(entry.Model, entry.Usage);
        var latencyMs = (int)Math.Clamp(entry.LatencyMs, 0, int.MaxValue);

        dbContext.AiUsageLogs.Add(AiUsageLog.Create(
            entry.TenantId,
            entry.UserId,
            entry.Model,
            entry.Purpose,
            entry.PromptVersion,
            entry.ReferenceId,
            entry.Usage.InputTokens,
            entry.Usage.OutputTokens,
            entry.Usage.CacheCreationTokens,
            entry.Usage.CacheReadTokens,
            cost,
            latencyMs,
            entry.Success,
            entry.ErrorCode,
            entry.RequestId,
            dateTimeProvider.UtcNow));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            throw new InvalidOperationException("Não foi possível registrar o uso de IA em ai_usage_logs.", exception);
        }

        logger.LogInformation(
            "IA: uso registrado — {Model}, {InputTokens} in / {OutputTokens} out, US$ {CostUsd}, sucesso={Success}.",
            entry.Model, entry.Usage.InputTokens, entry.Usage.OutputTokens, cost, entry.Success);
    }
}

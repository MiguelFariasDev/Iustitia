using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Aplica a política de retenção da outbox: mensagens processadas com mais de RetentionDays
/// e mensagens definitivamente falhadas com mais de RetentionDays*2 são removidas. Também
/// remove registros de idempotência (processed_events) com mais de 30 dias — ver ADR-035.
/// </summary>
public sealed class OutboxCleanupService(
    EfDbContext dbContext,
    IOptions<OutboxProcessorOptions> options,
    ILogger<OutboxCleanupService> logger) : IOutboxCleanupService
{
    private const int ProcessedEventRetentionDays = 30;

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var now = DateTimeOffset.UtcNow;
        var processedCutoff = now.AddDays(-settings.RetentionDays);
        var failedCutoff = now.AddDays(-(settings.RetentionDays * 2));

        var outboxMessagesToRemove = await dbContext.Set<OutboxMessage>()
            .Where(message =>
                (message.Status == OutboxStatus.Processed && message.ProcessedAt != null && message.ProcessedAt < processedCutoff)
                || (message.Status == OutboxStatus.Failed && message.CreatedAt < failedCutoff))
            .ToListAsync(cancellationToken);

        dbContext.Set<OutboxMessage>().RemoveRange(outboxMessagesToRemove);

        var processedEventsCutoff = now.AddDays(-ProcessedEventRetentionDays);
        var processedEventsToRemove = await dbContext.Set<ProcessedEvent>()
            .Where(processedEvent => processedEvent.ProcessedAt < processedEventsCutoff)
            .ToListAsync(cancellationToken);

        dbContext.Set<ProcessedEvent>().RemoveRange(processedEventsToRemove);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Outbox cleanup: {OutboxCount} mensagens e {ProcessedEventCount} registros de idempotência removidos.",
            outboxMessagesToRemove.Count, processedEventsToRemove.Count);

        return outboxMessagesToRemove.Count + processedEventsToRemove.Count;
    }
}

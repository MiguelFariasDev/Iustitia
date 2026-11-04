using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Jobs.Jobs;

/// <summary>Aplica a política de retenção da outbox e dos registros de idempotência (ver ADR-034/ADR-035).</summary>
public sealed class OutboxCleanupJob(IOutboxCleanupService outboxCleanupService, ILogger<OutboxCleanupJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var removedCount = await outboxCleanupService.CleanupAsync(cancellationToken);
        logger.LogInformation("OutboxCleanupJob: {RemovedCount} registros removidos.", removedCount);
    }
}

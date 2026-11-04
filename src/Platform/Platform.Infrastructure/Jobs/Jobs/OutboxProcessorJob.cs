using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Infrastructure.Jobs.Jobs;

/// <summary>Processa a outbox pendente (ver ADR-008/ADR-034). Erros relançam para o Hangfire aplicar retry.</summary>
public sealed class OutboxProcessorJob(IOutboxProcessor outboxProcessor, ILogger<OutboxProcessorJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var processedCount = await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);

        if (processedCount > 0)
        {
            logger.LogInformation("OutboxProcessorJob: {ProcessedCount} mensagens processadas.", processedCount);
        }
    }
}

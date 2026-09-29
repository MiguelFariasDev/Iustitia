namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;

/// <summary>Ledger de idempotência usado por ConsumerBase/IntegrationEventConsumer — ver ADR-035.</summary>
public interface IProcessedEventStore
{
    Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
}

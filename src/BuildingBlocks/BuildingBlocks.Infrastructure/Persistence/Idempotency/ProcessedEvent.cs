namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Registro de idempotência: marca que um consumer específico já processou um EventId,
/// para que reentregas do bus (at-least-once — ver ADR-034/ADR-035) sejam ignoradas em vez
/// de reprocessadas. TTL de 30 dias, removido pelo OutboxCleanupJob.
/// </summary>
public sealed class ProcessedEvent
{
    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string ConsumerName { get; private set; } = null!;

    public DateTimeOffset ProcessedAt { get; private set; }

    private ProcessedEvent()
    {
        // EF Core.
    }

    public ProcessedEvent(Guid eventId, string consumerName, DateTimeOffset processedAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        ConsumerName = consumerName;
        ProcessedAt = processedAt;
    }
}

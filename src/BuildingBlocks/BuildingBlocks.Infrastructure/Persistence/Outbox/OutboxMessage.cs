namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Registro de persistência do Outbox Pattern (ver ADR-008): eventos de domínio são
/// gravados aqui na mesma transação da mudança de estado (via OutboxInterceptor) e
/// publicados depois por um worker (ver <see cref="IOutboxProcessor"/>), garantindo
/// que nenhum evento seja perdido mesmo que a publicação falhe.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    public Guid AggregateId { get; private set; }

    public string EventType { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public OutboxStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    private OutboxMessage()
    {
        // EF Core.
    }

    public OutboxMessage(Guid aggregateId, string eventType, string payload, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        AggregateId = aggregateId;
        EventType = eventType;
        Payload = payload;
        CreatedAt = createdAt;
        Status = OutboxStatus.Pending;
        RetryCount = 0;
    }

    public void MarkAsProcessed(DateTimeOffset processedAt)
    {
        Status = OutboxStatus.Processed;
        ProcessedAt = processedAt;
    }

    public void MarkAsFailed() => RetryCount++;
}

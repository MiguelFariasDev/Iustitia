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

    public string? LastError { get; private set; }

    public DateTimeOffset? NextRetryAt { get; private set; }

    /// <summary>
    /// CorrelationId da requisição/job que originou o evento (ver LogEnrichmentContext),
    /// propagado ao publicar a mensagem via MassTransit — permite rastrear um fluxo de
    /// ponta a ponta mesmo atravessando a persistência assíncrona da outbox (ver ADR-037).
    /// </summary>
    public Guid? CorrelationId { get; private set; }

    private OutboxMessage()
    {
        // EF Core.
    }

    public OutboxMessage(Guid aggregateId, string eventType, string payload, DateTimeOffset createdAt, Guid? correlationId = null)
    {
        Id = Guid.NewGuid();
        AggregateId = aggregateId;
        EventType = eventType;
        Payload = payload;
        CreatedAt = createdAt;
        Status = OutboxStatus.Pending;
        RetryCount = 0;
        CorrelationId = correlationId;
    }

    public void MarkAsProcessed(DateTimeOffset processedAt)
    {
        Status = OutboxStatus.Processed;
        ProcessedAt = processedAt;
        LastError = null;
        NextRetryAt = null;
    }

    /// <summary>
    /// Registra uma falha de publicação com backoff exponencial. Permanece <see cref="OutboxStatus.Pending"/>
    /// (será retentada em <paramref name="nextRetryAt"/>) até esgotar <paramref name="maxRetries"/>,
    /// quando passa a <see cref="OutboxStatus.Failed"/> definitivamente (exige intervenção/replay manual).
    /// </summary>
    public void MarkAsFailed(string error, DateTimeOffset nextRetryAt, int maxRetries)
    {
        RetryCount++;
        LastError = error;
        NextRetryAt = nextRetryAt;

        if (RetryCount >= maxRetries)
        {
            Status = OutboxStatus.Failed;
        }
    }
}

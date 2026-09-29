using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Events;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;
using Microsoft.Extensions.Logging;

namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Especialização de <see cref="ConsumerBase{TMessage}"/> para eventos de integração:
/// extrai o TenantId automaticamente (todo IIntegrationEvent tem um) e injeta no contexto
/// do handler concreto.
/// </summary>
public abstract class IntegrationEventConsumer<TEvent>(
    IProcessedEventStore processedEventStore,
    IAppMetrics metrics,
    ILogger<IntegrationEventConsumer<TEvent>> logger)
    : ConsumerBase<TEvent>(processedEventStore, metrics, logger)
    where TEvent : class, IIntegrationEvent
{
    protected sealed override Task HandleAsync(TEvent message, CancellationToken cancellationToken) =>
        HandleIntegrationEventAsync(message, message.TenantId, cancellationToken);

    protected abstract Task HandleIntegrationEventAsync(TEvent message, Guid tenantId, CancellationToken cancellationToken);
}

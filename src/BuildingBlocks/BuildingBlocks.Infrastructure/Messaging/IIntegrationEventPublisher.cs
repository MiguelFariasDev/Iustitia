using Advocacia.BuildingBlocks.Domain.Events;

namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Publica eventos de integração diretamente via MassTransit — usado por serviços que não
/// passam pelo Outbox Pattern porque a entidade de origem não é um AggregateRoot com
/// domain events (ex.: AuditLog, ver AuditService/AuditLogCreatedEvent).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}

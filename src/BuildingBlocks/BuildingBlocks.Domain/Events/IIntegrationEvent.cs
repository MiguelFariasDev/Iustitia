using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.BuildingBlocks.Domain.Events;

/// <summary>
/// Evento de integração: contrato público, cruzando os limites de um módulo/processo via
/// mensageria (MassTransit/Azure Service Bus — ADR-016), diferente de <see cref="IDomainEvent"/>,
/// que é interno ao agregado que o levantou. Estende IDomainEvent (reaproveita EventId/OccurredOn)
/// e acrescenta TenantId (todo evento de integração pertence a um tenant) e EventType (nome
/// completo do tipo, usado para roteamento/observabilidade e para reconstrução pelo
/// resolvedor de tipos do OutboxProcessor, em BuildingBlocks.Infrastructure).
/// </summary>
public interface IIntegrationEvent : IDomainEvent
{
    Guid TenantId { get; }

    string EventType { get; }
}

/// <summary>Base opcional para eventos de integração concretos (ex.: AuditLogCreatedEvent).</summary>
public abstract record IntegrationEvent(Guid TenantId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().FullName ?? GetType().Name;
}

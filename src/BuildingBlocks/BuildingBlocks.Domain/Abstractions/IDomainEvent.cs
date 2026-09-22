namespace Advocacia.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Evento de domínio: um fato relevante que já ocorreu dentro de um agregado.
/// Não depende de nenhuma biblioteca de mediator — a ponte com MediatR/mensageria
/// fica na camada de Application/Infrastructure (ver BuildingBlocks.Application).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// Base opcional para eventos de domínio concretos, evitando repetir o boilerplate
/// de EventId/OccurredOn em cada evento (ex.: TenantCreatedEvent, UserInvitedEvent).
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

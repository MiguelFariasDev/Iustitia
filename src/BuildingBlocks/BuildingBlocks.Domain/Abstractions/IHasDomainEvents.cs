namespace Advocacia.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Contrato para agregados que acumulam eventos de domínio até serem
/// despachados (tipicamente no SaveChanges do EF Core, via interceptor).
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

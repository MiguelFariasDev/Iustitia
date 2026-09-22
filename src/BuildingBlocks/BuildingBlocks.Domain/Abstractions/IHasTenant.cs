namespace Advocacia.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Marca entidades pertencentes a um tenant (escritório). O filtro global de
/// isolamento multi-tenant é aplicado no BaseDbContext a partir de ITenantContext
/// (ver BuildingBlocks.Application/Infrastructure) e reforçado por Row-Level Security no Postgres.
/// </summary>
public interface IHasTenant
{
    Guid TenantId { get; }
}

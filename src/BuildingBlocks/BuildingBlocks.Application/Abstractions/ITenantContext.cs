namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Contexto de tenant resolvido para a requisição atual. Usado pelo BaseDbContext para
/// aplicar o filtro global de isolamento multi-tenant (reforçado por RLS no Postgres) —
/// ver TenantContextMiddleware em Platform.Api e Platform.Infrastructure.
/// </summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    Guid TenantId { get; }

    void SetTenant(Guid tenantId);
}

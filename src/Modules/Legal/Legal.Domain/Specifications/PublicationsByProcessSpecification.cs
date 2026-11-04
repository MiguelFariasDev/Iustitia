using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Domain.Specifications;

/// <summary>
/// Publicações de um processo do escritório, mais recentes primeiro — a consulta da
/// timeline do processo. Serve exatamente o índice
/// (tenant_id, process_id, published_at DESC) descrito na ADR-057.
/// </summary>
public sealed class PublicationsByProcessSpecification : Specification<Publication>
{
    public PublicationsByProcessSpecification(Guid tenantId, Guid processId, int? skip = null, int? take = null)
        : base(publication => publication.TenantId == tenantId && publication.ProcessId == processId)
    {
        ApplyOrderByDescending(publication => publication.PublishedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }
}

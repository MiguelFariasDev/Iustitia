using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Domain.Specifications;

/// <summary>
/// Publicações de um escritório, mais recentes primeiro, com filtros opcionais de status e
/// de leitura. O <c>tenantId</c> aparece explicitamente no critério mesmo havendo filtro
/// global por tenant (BaseDbContext) e RLS no Postgres: as três camadas são independentes e
/// nenhuma delas deve ser a única a impedir um vazamento (ver ADR-004).
///
/// A ordenação por published_at DESC casa com o índice
/// (tenant_id, process_id, published_at DESC) — ver ADR-057.
/// </summary>
public sealed class PublicationsByTenantSpecification : Specification<Publication>
{
    public PublicationsByTenantSpecification(
        Guid tenantId,
        PublicationStatus? status = null,
        bool? isRead = null,
        int? skip = null,
        int? take = null)
        : base(publication =>
            publication.TenantId == tenantId
            && (status == null || publication.Status == status)
            && (isRead == null || publication.IsRead == isRead))
    {
        // Ordena pelo value object, não por PublishedAt.Value: a propriedade é mapeada para uma
        // única coluna via HasConversion, e o tradutor do EF Core não sabe alcançar um membro
        // DENTRO de um valor convertido — ordenar por .Value falha em runtime.
        ApplyOrderByDescending(publication => publication.PublishedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }
}

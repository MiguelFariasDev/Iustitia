using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Domain.Specifications;

/// <summary>
/// Publicações aguardando revisão humana, MAIS ANTIGAS primeiro — o oposto das outras
/// specifications de propósito: aqui a ordem é a da fila de trabalho, e o que espera há
/// mais tempo é o que corre mais risco de perda de prazo.
///
/// Serve o índice parcial (tenant_id, process_id) WHERE status = 'PendingReview'
/// (ver ADR-057). A fila priorizada de verdade (peso por prazo, responsável, urgência) é da
/// Fase 3 — esta specification é só a leitura crua do que está pendente.
/// </summary>
public sealed class PublicationsPendingReviewSpecification : Specification<Publication>
{
    public PublicationsPendingReviewSpecification(Guid tenantId, int? skip = null, int? take = null)
        : base(publication =>
            publication.TenantId == tenantId && publication.Status == PublicationStatus.PendingReview)
    {
        ApplyOrderBy(publication => publication.PublishedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }
}

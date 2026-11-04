using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Repositório especializado de <see cref="Publication"/>. Estende o genérico
/// (<see cref="IRepository{TEntity, TId}"/>) com as consultas próprias do domínio de
/// publicações — sobretudo as de deduplicação por ExternalId, que são o caminho crítico do
/// job de captura e não caberiam numa specification genérica sem perder o índice único.
/// </summary>
public interface IPublicationRepository : IRepository<Publication, PublicationId>
{
    Task<Publication?> GetByExternalIdAsync(
        Guid tenantId, string externalId, PublicationSource source, CancellationToken cancellationToken = default);

    Task<bool> ExistsByExternalIdAsync(
        Guid tenantId, string externalId, PublicationSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quais dos ExternalIds informados já existem para o tenant. Uma única ida ao banco
    /// para a página inteira devolvida pelo CNJ — com 100 itens por página, checar um a um
    /// custaria 100 round-trips por página (ver CnjCaptureJob).
    /// </summary>
    Task<IReadOnlySet<string>> FindExistingExternalIdsAsync(
        Guid tenantId, IReadOnlyCollection<string> externalIds, PublicationSource source, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Publication>> ListByProcessAsync(
        Guid tenantId, Guid processId, int skip, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Publication>> ListByTenantAsync(
        Guid tenantId, PublicationStatus? status, bool? isRead, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountByTenantAsync(
        Guid tenantId, PublicationStatus? status, bool? isRead, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Publication>> ListPendingReviewAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Busca com os filtros da tela de publicações, mais recentes primeiro.</summary>
    Task<IReadOnlyList<Publication>> SearchAsync(
        Guid tenantId, PublicationSearchCriteria criteria, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountSearchAsync(
        Guid tenantId, PublicationSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Contadores do topo da tela, em uma ida ao banco.</summary>
    Task<PublicationCounters> GetCountersAsync(
        Guid tenantId, DateOnly today, DateOnly weekStart, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<Publication> publications);
}

/// <summary>
/// Filtros da listagem. <paramref name="Statuses"/> vazio = qualquer status.
/// <paramref name="Busca"/> casa com número do processo (dígitos, parcial) ou nome/documento do cliente.
/// <paramref name="From"/>/<paramref name="To"/> são datas de divulgação, inclusivas.
/// </summary>
public sealed record PublicationSearchCriteria(
    IReadOnlyCollection<PublicationStatus>? Statuses = null,
    bool? IsRead = null,
    Guid? ProcessId = null,
    Guid? ClienteId = null,
    Guid? ResponsavelUserId = null,
    string? Tribunal = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Busca = null,
    PublicationSort Sort = PublicationSort.Recent);

/// <summary>Ordenações da listagem. Desempate sempre pela captura mais recente.</summary>
public enum PublicationSort
{
    Recent,
    Oldest,
    TribunalAsc,
    TribunalDesc,
}

/// <param name="Total">Todas as publicações.</param>
/// <param name="PendingReview">Não tratadas.</param>
/// <param name="Unread">Não lidas.</param>
/// <param name="PendingToday">Não tratadas divulgadas hoje.</param>
/// <param name="TreatedThisWeek">Tratadas (aprovadas, corrigidas ou arquivadas) com divulgação na semana.</param>
public sealed record PublicationCounters(int Total, int PendingReview, int Unread, int PendingToday, int TreatedThisWeek);

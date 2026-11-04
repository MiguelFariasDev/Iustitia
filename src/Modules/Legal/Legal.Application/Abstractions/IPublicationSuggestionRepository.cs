using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;

namespace Advocacia.Modules.Legal.Application.Abstractions;

/// <summary>
/// Persistência das sugestões de IA. Não estende o repositório genérico porque
/// <see cref="PublicationSuggestion"/> não é raiz de agregado: é um registro imutável,
/// gravado uma vez e lido pela revisão humana (Etapa 3.3).
/// </summary>
public interface IPublicationSuggestionRepository
{
    void Add(PublicationSuggestion suggestion);

    Task<PublicationSuggestion?> GetByPublicationIdAsync(
        Guid tenantId, PublicationId publicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publicações PendingReview do escritório que ainda não têm sugestão, as mais antigas
    /// primeiro (o que espera há mais tempo corre mais risco de perder prazo).
    /// </summary>
    Task<IReadOnlyList<Publication>> ListAwaitingSuggestionAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quantas chamadas de classificação já voltaram com resposta INVÁLIDA (e foram cobradas)
    /// para cada publicação, lidas de ai_usage_logs. O job usa para não reprocessar para sempre
    /// — e pagar de novo — uma publicação cuja resposta o modelo nunca acerta.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountInvalidResponseAttemptsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> publicationIds, CancellationToken cancellationToken = default);

    /// <summary>Escritórios com ao menos uma publicação pendente sem sugestão — o job itera sobre eles.</summary>
    Task<IReadOnlyList<Guid>> ListTenantsAwaitingSuggestionAsync(CancellationToken cancellationToken = default);
}

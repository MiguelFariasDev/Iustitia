using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Ai;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório de <see cref="PublicationSuggestion"/>. Como nos demais, o tenantId é repetido no
/// Where mesmo com o filtro global e o RLS ativos (ADR-004).
/// </summary>
public sealed class PublicationSuggestionRepository(LegalDbContext dbContext) : IPublicationSuggestionRepository
{
    public void Add(PublicationSuggestion suggestion) => dbContext.PublicationSuggestions.Add(suggestion);

    public Task<PublicationSuggestion?> GetByPublicationIdAsync(
        Guid tenantId, PublicationId publicationId, CancellationToken cancellationToken = default) =>
        dbContext.PublicationSuggestions.FirstOrDefaultAsync(
            suggestion => suggestion.TenantId == tenantId && suggestion.PublicationId == publicationId,
            cancellationToken);

    public async Task<IReadOnlyList<Publication>> ListAwaitingSuggestionAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken = default) =>
        await dbContext.Publications
            .Where(publication => publication.TenantId == tenantId
                && publication.Status == PublicationStatus.PendingReview
                && !dbContext.PublicationSuggestions.Any(suggestion => suggestion.PublicationId == publication.Id))
            .OrderBy(publication => publication.PublishedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountInvalidResponseAttemptsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> publicationIds, CancellationToken cancellationToken = default)
    {
        if (publicationIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = publicationIds.Select(id => (Guid?)id).ToList();

        var counts = await dbContext.AiUsageLogs
            .Where(log => log.TenantId == tenantId
                && log.Purpose == "classification"
                && log.Status == AiCallStatus.InvalidResponse
                && ids.Contains(log.ReferenceId))
            .GroupBy(log => log.ReferenceId)
            .Select(group => new { PublicationId = group.Key, Attempts = group.Count() })
            .ToListAsync(cancellationToken);

        return counts
            .Where(row => row.PublicationId.HasValue)
            .ToDictionary(row => row.PublicationId!.Value, row => row.Attempts);
    }

    /// <summary>
    /// IgnoreQueryFilters é necessário e correto aqui, como em ListTenantsComProcessosAtivosAsync:
    /// o job precisa descobrir QUAIS escritórios têm trabalho antes de poder definir um tenant.
    /// O filtro de soft delete é reaplicado à mão.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListTenantsAwaitingSuggestionAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Publications
            .IgnoreQueryFilters()
            .Where(publication => !publication.IsDeleted
                && publication.Status == PublicationStatus.PendingReview
                && !dbContext.PublicationSuggestions
                    .IgnoreQueryFilters()
                    .Any(suggestion => suggestion.PublicationId == publication.Id))
            .Select(publication => publication.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
}

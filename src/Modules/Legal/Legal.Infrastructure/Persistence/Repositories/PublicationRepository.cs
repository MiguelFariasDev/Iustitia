using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório de <see cref="Publication"/> sobre o <see cref="LegalDbContext"/>. Herda do
/// genérico para não reimplementar GetById/Add/Update e acrescenta as consultas de
/// deduplicação e as listagens do domínio.
///
/// Todas as consultas recebem o tenantId explicitamente e o repetem no Where, mesmo com o
/// filtro global do BaseDbContext e o RLS do Postgres ativos — três camadas independentes,
/// nenhuma sendo a única linha de defesa (ver ADR-004).
/// </summary>
public sealed class PublicationRepository(LegalDbContext dbContext)
    : Repository<Publication, PublicationId>(dbContext), IPublicationRepository
{
    public Task<Publication?> GetByExternalIdAsync(
        Guid tenantId, string externalId, PublicationSource source, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            publication => publication.TenantId == tenantId
                && publication.ExternalId == externalId
                && publication.Source == source,
            cancellationToken);

    public Task<bool> ExistsByExternalIdAsync(
        Guid tenantId, string externalId, PublicationSource source, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(
            publication => publication.TenantId == tenantId
                && publication.ExternalId == externalId
                && publication.Source == source,
            cancellationToken);

    /// <summary>
    /// Um único SELECT ... WHERE external_id = ANY(...) para a página inteira devolvida pelo
    /// CNJ. Projeta só a coluna external_id: carregar as entidades completas traria o
    /// raw_content de centenas de publicações para a memória sem nenhuma necessidade.
    /// </summary>
    public async Task<IReadOnlySet<string>> FindExistingExternalIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> externalIds,
        PublicationSource source,
        CancellationToken cancellationToken = default)
    {
        if (externalIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var found = await DbSet
            .Where(publication => publication.TenantId == tenantId
                && publication.Source == source
                && externalIds.Contains(publication.ExternalId))
            .Select(publication => publication.ExternalId)
            .ToListAsync(cancellationToken);

        return found.ToHashSet(StringComparer.Ordinal);
    }

    public Task<IReadOnlyList<Publication>> ListByProcessAsync(
        Guid tenantId, Guid processId, int skip, int take, CancellationToken cancellationToken = default) =>
        ListAsync(new PublicationsByProcessSpecification(tenantId, processId, skip, take), cancellationToken);

    public Task<IReadOnlyList<Publication>> ListByTenantAsync(
        Guid tenantId,
        PublicationStatus? status,
        bool? isRead,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        ListAsync(new PublicationsByTenantSpecification(tenantId, status, isRead, skip, take), cancellationToken);

    public Task<int> CountByTenantAsync(
        Guid tenantId, PublicationStatus? status, bool? isRead, CancellationToken cancellationToken = default) =>
        CountAsync(new PublicationsByTenantSpecification(tenantId, status, isRead), cancellationToken);

    public Task<IReadOnlyList<Publication>> ListPendingReviewAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken = default) =>
        ListAsync(new PublicationsPendingReviewSpecification(tenantId, skip, take), cancellationToken);

    public async Task<IReadOnlyList<Publication>> SearchAsync(
        Guid tenantId, PublicationSearchCriteria criteria, int skip, int take, CancellationToken cancellationToken = default) =>
        await Order(await ApplySearchAsync(tenantId, criteria, cancellationToken), criteria.Sort)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    private static IOrderedQueryable<Publication> Order(IQueryable<Publication> query, PublicationSort sort) => sort switch
    {
        PublicationSort.Oldest => query.OrderBy(p => p.PublishedOn).ThenBy(p => p.CapturedAt),
        PublicationSort.TribunalAsc => query.OrderBy(p => p.Tribunal).ThenByDescending(p => p.PublishedOn),
        PublicationSort.TribunalDesc => query.OrderByDescending(p => p.Tribunal).ThenByDescending(p => p.PublishedOn),
        _ => query.OrderByDescending(p => p.PublishedOn).ThenByDescending(p => p.CapturedAt),
    };

    public async Task<int> CountSearchAsync(
        Guid tenantId, PublicationSearchCriteria criteria, CancellationToken cancellationToken = default) =>
        await (await ApplySearchAsync(tenantId, criteria, cancellationToken)).CountAsync(cancellationToken);

    public async Task<PublicationCounters> GetCountersAsync(
        Guid tenantId, DateOnly today, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        // Uma agregação com contagens condicionais (um único passe pelo índice do tenant) em
        // vez de cinco consultas — o filtro por tenant já limita o conjunto.
        var row = await DbSet
            .Where(publication => publication.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Pending = group.Count(p => p.Status == PublicationStatus.PendingReview),
                Unread = group.Count(p => !p.IsRead),
                PendingToday = group.Count(p => p.Status == PublicationStatus.PendingReview && p.PublishedOn == today),
                TreatedWeek = group.Count(p => p.Status != PublicationStatus.PendingReview
                    && p.Status != PublicationStatus.Rejected && p.PublishedOn >= weekStart),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? new PublicationCounters(0, 0, 0, 0, 0)
            : new PublicationCounters(row.Total, row.Pending, row.Unread, row.PendingToday, row.TreatedWeek);
    }

    /// <summary>
    /// Filtros estruturados + busca. A busca textual consulta colunas DERIVADAS (CnjDigits,
    /// NomeBusca, DocumentoBusca) e nunca o value object — ver QueryTranslationTests. O cliente
    /// é resolvido por subconsulta sobre clientes, evitando um join que multiplicaria linhas.
    /// </summary>
    private async Task<IQueryable<Publication>> ApplySearchAsync(
        Guid tenantId, PublicationSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = DbSet.Where(publication => publication.TenantId == tenantId);

        if (criteria.Statuses is { Count: > 0 } statuses)
        {
            query = query.Where(publication => statuses.Contains(publication.Status));
        }

        if (criteria.IsRead is { } isRead)
        {
            query = query.Where(publication => publication.IsRead == isRead);
        }

        if (criteria.ProcessId is { } processId)
        {
            query = query.Where(publication => publication.ProcessId == processId);
        }

        if (criteria.ClienteId is { } clienteId)
        {
            query = query.Where(publication => publication.ClienteId == clienteId);
        }

        if (criteria.ResponsavelUserId is { } responsavelId)
        {
            query = query.Where(publication => publication.ResponsavelUserId == responsavelId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Tribunal))
        {
            var tribunal = criteria.Tribunal.Trim().ToUpperInvariant();
            query = query.Where(publication => publication.Tribunal == tribunal);
        }

        if (criteria.From is { } from)
        {
            query = query.Where(publication => publication.PublishedOn >= from);
        }

        if (criteria.To is { } to)
        {
            query = query.Where(publication => publication.PublishedOn <= to);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Busca))
        {
            var termo = criteria.Busca.Trim().ToLowerInvariant();
            var digitos = new string(termo.Where(char.IsDigit).ToArray());

            // Clientes resolvidos ANTES, em consulta própria: comparar o Id (value object
            // convertido) dentro de uma subconsulta não traduz para SQL. 500 é teto de
            // segurança — um termo que casa mais que isso é amplo demais para ser útil.
            //
            // Documento: só igualdade com CPF/CNPJ completo (11/14 dígitos), via índice cego —
            // o documento é cifrado em repouso (ADR-068). Entre 3 e 10 dígitos o termo é um
            // fragmento de número CNJ e não procura cliente nenhum.
            var clientes = dbContext.Set<Cliente>().Where(cliente => cliente.TenantId == tenantId);
            var procuraClientePorDocumento = digitos.Length is 11 or 14;
            var procuraClientePorNome = digitos.Length < 3;
            var clienteIds = procuraClientePorDocumento || procuraClientePorNome
                ? await (procuraClientePorDocumento
                        ? clientes.Where(cliente => cliente.DocumentoBusca == digitos)
                        : clientes.Where(cliente => cliente.NomeBusca.Contains(termo)))
                    .Select(cliente => cliente.Id)
                    .Take(500)
                    .ToListAsync(cancellationToken)
                : [];
            var clienteGuids = clienteIds.Select(id => id.Value).ToList();

            query = digitos.Length >= 3
                ? query.Where(publication =>
                    publication.CnjDigits.Contains(digitos)
                    || (publication.ClienteId != null && clienteGuids.Contains(publication.ClienteId.Value)))
                : query.Where(publication =>
                    publication.ClienteId != null && clienteGuids.Contains(publication.ClienteId.Value));
        }

        return query;
    }

    public void AddRange(IEnumerable<Publication> publications) => DbSet.AddRange(publications);
}

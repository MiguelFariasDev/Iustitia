using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.CRM.Application.Abstractions;
using Advocacia.Modules.CRM.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.CRM.Infrastructure.Persistence.Repositories;

public sealed class ContactRepository(CrmDbContext dbContext)
    : Repository<Contact, ContactId>(dbContext), IContactRepository
{
    public async Task<IReadOnlyList<Contact>> ListAsync(
        Guid tenantId, ContactType? type, bool? onlyActive, string? search, int skip, int take,
        CancellationToken cancellationToken = default) =>
        await ApplySearch(BaseQuery(tenantId, type, onlyActive), search)
            .OrderBy(contact => contact.NameSearch)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        Guid tenantId, ContactType? type, bool? onlyActive, string? search,
        CancellationToken cancellationToken = default) =>
        ApplySearch(BaseQuery(tenantId, type, onlyActive), search).CountAsync(cancellationToken);

    private IQueryable<Contact> BaseQuery(Guid tenantId, ContactType? type, bool? onlyActive) =>
        DbSet.Where(contact =>
            contact.TenantId == tenantId
            && (type == null || contact.Type == type)
            && (onlyActive == null || contact.IsActive == onlyActive));

    /// <summary>Termo com 3+ dígitos busca o documento por prefixo; qualquer outro, o nome (coluna derivada).</summary>
    private static IQueryable<Contact> ApplySearch(IQueryable<Contact> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var term = search.Trim().ToLowerInvariant();
        var digits = new string(term.Where(char.IsDigit).ToArray());

        return digits.Length >= 3
            ? query.Where(contact => contact.Document != null && contact.Document.StartsWith(digits))
            : query.Where(contact => contact.NameSearch.Contains(term));
    }
}

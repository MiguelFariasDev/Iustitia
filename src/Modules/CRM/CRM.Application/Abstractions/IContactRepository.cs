using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.CRM.Domain.Contacts;

namespace Advocacia.Modules.CRM.Application.Abstractions;

public interface IContactRepository : IRepository<Contact, ContactId>
{
    Task<IReadOnlyList<Contact>> ListAsync(
        Guid tenantId, ContactType? type, bool? onlyActive, string? search, int skip, int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid tenantId, ContactType? type, bool? onlyActive, string? search,
        CancellationToken cancellationToken = default);
}

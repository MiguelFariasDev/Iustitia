using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.CRM.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.CRM.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo CRM — um por módulo, mesmo banco físico (ADR-001). Filtros globais de
/// soft delete e tenant vêm do <see cref="BaseDbContext"/>. Sem OutboxMessages: o módulo ainda
/// não levanta eventos de domínio (ver Contact).
/// </summary>
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<Contact> Contacts => Set<Contact>();
}

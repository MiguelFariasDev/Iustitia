using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence;

/// <summary>DbContext do módulo Drafting — um por módulo, mesmo banco físico (ADR-001).</summary>
public sealed class DraftingDbContext(DbContextOptions<DraftingDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<LegalDocumentRequest> LegalDocumentRequests => Set<LegalDocumentRequest>();

    public DbSet<RetrievedSource> RetrievedSources => Set<RetrievedSource>();

    public DbSet<LegalDocumentEvent> LegalDocumentEvents => Set<LegalDocumentEvent>();
}

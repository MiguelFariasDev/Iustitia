using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Financeiro.Domain.Lancamentos;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Financeiro.Infrastructure.Persistence;

/// <summary>DbContext do módulo Financeiro — um por módulo, mesmo banco físico (ADR-001).</summary>
public sealed class FinanceiroDbContext(DbContextOptions<FinanceiroDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
}

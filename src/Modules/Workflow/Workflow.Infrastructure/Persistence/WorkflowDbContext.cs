using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Workflow.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence;

/// <summary>DbContext do módulo Workflow — um por módulo, mesmo banco físico (ADR-001).</summary>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
}

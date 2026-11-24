using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Workflow.Domain.Calendar;
using Advocacia.Modules.Workflow.Domain.Tags;
using Advocacia.Modules.Workflow.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence;

/// <summary>DbContext do módulo Workflow — um por módulo, mesmo banco físico (ADR-001).</summary>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TagAssignment> TagAssignments => Set<TagAssignment>();
}

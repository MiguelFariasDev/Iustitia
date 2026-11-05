using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Agenda;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;

public sealed class CalendarEventRepository(WorkflowDbContext dbContext)
    : Repository<CalendarEvent, CalendarEventId>(dbContext), ICalendarEventRepository
{
    public async Task<IReadOnlyList<CalendarEvent>> ListStartingBetweenAsync(
        Guid tenantId, Guid ownerId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(calendarEvent => calendarEvent.TenantId == tenantId && calendarEvent.OwnerUserId == ownerId
                && calendarEvent.StartsAt >= fromUtc && calendarEvent.StartsAt < toUtcExclusive)
            .OrderBy(calendarEvent => calendarEvent.StartsAt)
            .ToListAsync(cancellationToken);
}

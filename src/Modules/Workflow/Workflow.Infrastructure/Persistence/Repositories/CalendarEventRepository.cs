using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;

public sealed class CalendarEventRepository(WorkflowDbContext dbContext)
    : Repository<CalendarEvent, CalendarEventId>(dbContext), ICalendarEventRepository
{
    public async Task<IReadOnlyList<CalendarEvent>> ListByPeriodAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CalendarEventType? type,
        Guid? assignedUserId,
        Guid? processId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(calendarEvent =>
                calendarEvent.TenantId == tenantId
                && calendarEvent.StartAt < to
                && calendarEvent.EndAt >= from
                && (type == null || calendarEvent.Type == type)
                && (assignedUserId == null || calendarEvent.AssignedUserId == assignedUserId)
                && (processId == null || calendarEvent.ProcessId == processId))
            .OrderBy(calendarEvent => calendarEvent.StartAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// IgnoreQueryFilters é necessário e correto: o job roda fora de qualquer requisição, sem
    /// tenant — o filtro global sozinho devolveria tudo e o RLS (sem app.current_tenant)
    /// nada. O soft delete é reaplicado à mão, que é o que o IgnoreQueryFilters desliga.
    /// </summary>
    public async Task<IReadOnlyList<CalendarEvent>> ListWithDueRemindersAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var dayLimit = now.AddDays(1);
        var hourLimit = now.AddHours(1);

        return await DbSet
            .IgnoreQueryFilters()
            .Where(calendarEvent =>
                !calendarEvent.IsDeleted
                && calendarEvent.AssignedUserId != null
                && calendarEvent.StartAt > now
                && ((calendarEvent.OneDayReminderSentAt == null && calendarEvent.StartAt <= dayLimit)
                    || (calendarEvent.OneHourReminderSentAt == null && calendarEvent.StartAt <= hourLimit)))
            .OrderBy(calendarEvent => calendarEvent.StartAt)
            .ToListAsync(cancellationToken);
    }
}

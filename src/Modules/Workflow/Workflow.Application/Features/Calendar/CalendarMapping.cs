using Advocacia.Modules.Workflow.Domain.Calendar;

namespace Advocacia.Modules.Workflow.Application.Features.Calendar;

internal static class CalendarMapping
{
    internal static CalendarEventResponse ToResponse(this CalendarEvent calendarEvent) => new(
        calendarEvent.Id.Value,
        calendarEvent.Title,
        calendarEvent.Description,
        calendarEvent.Type.ToString(),
        calendarEvent.StartAt,
        calendarEvent.EndAt,
        calendarEvent.AllDay,
        calendarEvent.Location,
        calendarEvent.ProcessId,
        calendarEvent.AssignedUserId);
}

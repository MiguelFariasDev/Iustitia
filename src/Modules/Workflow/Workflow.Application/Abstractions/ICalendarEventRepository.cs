using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Calendar;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

public interface ICalendarEventRepository : IRepository<CalendarEvent, CalendarEventId>
{
    /// <summary>Eventos que INTERSECTAM o intervalo [from, to), ordenados por início.</summary>
    Task<IReadOnlyList<CalendarEvent>> ListByPeriodAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CalendarEventType? type,
        Guid? assignedUserId,
        Guid? processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Eventos futuros com lembrete devido em <paramref name="now"/>, de TODOS os escritórios
    /// (usado só pelo job, que roda fora de requisição — ignora o filtro de tenant).
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> ListWithDueRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

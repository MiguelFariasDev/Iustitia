using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Agenda;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

/// <summary>Eventos do DONO (tenant E usuário): compromisso é pessoal nesta etapa.</summary>
public interface ICalendarEventRepository : IRepository<CalendarEvent, CalendarEventId>
{
    /// <summary>Eventos que COMEÇAM em [fromUtc, toUtcExclusive).</summary>
    Task<IReadOnlyList<CalendarEvent>> ListStartingBetweenAsync(
        Guid tenantId, Guid ownerId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default);
}

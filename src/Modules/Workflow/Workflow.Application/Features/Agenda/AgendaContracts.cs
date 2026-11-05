using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Modules.Workflow.Domain.Agenda;
using Advocacia.Modules.Workflow.Domain.WorkTasks;

namespace Advocacia.Modules.Workflow.Application.Features.Agenda;

/// <summary>Um item da agenda: um evento com horário ou um prazo (tarefa) que ocupa o dia inteiro.</summary>
/// <param name="Id">Id do evento ou da tarefa.</param>
/// <param name="Kind"><c>Event</c> (compromisso com horário) ou <c>Task</c> (prazo).</param>
/// <param name="Type"><c>Hearing</c>, <c>Meeting</c>, <c>Other</c> (eventos) ou <c>Deadline</c> (prazos).</param>
/// <param name="Title">Título.</param>
/// <param name="Date">Dia da agenda (AAAA-MM-DD), no fuso do escritório. É por ele que o app agrupa por dia.</param>
/// <param name="StartsAt">Início (instante UTC); nulo para prazos, que não têm horário.</param>
/// <param name="EndsAt">Fim (instante UTC), se informado.</param>
/// <param name="AllDay">Verdadeiro para prazos.</param>
/// <param name="Location">Local, para eventos.</param>
/// <param name="ProcessoId">Processo relacionado, se houver.</param>
/// <param name="Status">Só para prazos: <c>Pending</c> ou <c>Completed</c>.</param>
public sealed record AgendaItemResponse(
    Guid Id,
    string Kind,
    string Type,
    string Title,
    DateOnly Date,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool AllDay,
    string? Location,
    Guid? ProcessoId,
    string? Status);

/// <param name="Start">Primeiro dia (inclusive).</param>
/// <param name="End">Último dia (inclusive).</param>
/// <param name="Items">Itens ordenados por dia; no dia, prazos primeiro e depois os eventos por horário.</param>
public sealed record AgendaResponse(DateOnly Start, DateOnly End, IReadOnlyList<AgendaItemResponse> Items);

/// <param name="Start">Primeiro dia (AAAA-MM-DD, inclusive).</param>
/// <param name="End">Último dia (inclusive). O intervalo tem no máximo 62 dias (dois meses de calendário).</param>
public sealed record GetAgendaQuery(DateOnly Start, DateOnly End) : IQuery<AgendaResponse>;

/// <param name="Title">Obrigatório, até 200 caracteres.</param>
/// <param name="Type"><c>Hearing</c> (audiência), <c>Meeting</c> (reunião) ou <c>Other</c>.</param>
/// <param name="StartsAt">Início, com fuso (ex.: <c>2026-11-13T14:00:00-03:00</c>).</param>
/// <param name="EndsAt">Fim (opcional); não pode ser antes do início.</param>
/// <param name="Location">Local (opcional).</param>
/// <param name="Description">Detalhe (opcional).</param>
/// <param name="ProcessoId">Processo relacionado (opcional).</param>
public sealed record CreateCalendarEventCommand(
    string Title,
    string Type,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? Description,
    Guid? ProcessoId) : ICommand<AgendaItemResponse>;

internal static class AgendaMapping
{
    internal static AgendaItemResponse From(CalendarEvent calendarEvent, BusinessCalendar calendar) => new(
        calendarEvent.Id.Value,
        "Event",
        calendarEvent.Type.ToString(),
        calendarEvent.Title,
        calendar.LocalDate(calendarEvent.StartsAt),
        calendarEvent.StartsAt,
        calendarEvent.EndsAt,
        AllDay: false,
        calendarEvent.Location,
        calendarEvent.ProcessoId,
        Status: null);

    internal static AgendaItemResponse From(WorkTask task) => new(
        task.Id.Value,
        "Task",
        "Deadline",
        task.Title,
        task.DueDate,
        StartsAt: null,
        EndsAt: null,
        AllDay: true,
        Location: null,
        task.ProcessoId,
        task.Status.ToString());
}

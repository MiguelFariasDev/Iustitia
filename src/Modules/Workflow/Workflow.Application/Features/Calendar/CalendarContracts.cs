using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Workflow.Application.Features.Calendar;

/// <summary><c>Type</c> é "Prazo", "Audiencia", "Reuniao" ou "Tarefa".</summary>
public sealed record CreateCalendarEventCommand(
    string Title,
    string Type,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt = null,
    bool AllDay = false,
    string? Description = null,
    string? Location = null,
    Guid? ProcessId = null,
    Guid? AssignedUserId = null) : ICommand<CalendarEventResponse>;

public sealed record UpdateCalendarEventCommand(
    Guid EventId,
    string Title,
    string Type,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt = null,
    bool AllDay = false,
    string? Description = null,
    string? Location = null,
    Guid? ProcessId = null,
    Guid? AssignedUserId = null) : ICommand;

/// <summary>Arrastar e soltar: só muda o período. Sem <c>EndAt</c>, a duração é preservada.</summary>
public sealed record RescheduleCalendarEventCommand(
    Guid EventId,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt = null,
    bool? AllDay = null) : ICommand;

public sealed record DeleteCalendarEventCommand(Guid EventId) : ICommand;

public sealed record GetCalendarEventByIdQuery(Guid EventId) : IQuery<CalendarEventResponse>;

public sealed record ListCalendarEventsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string? Type = null,
    Guid? AssignedUserId = null,
    Guid? ProcessId = null) : IQuery<ListCalendarEventsResponse>;

public sealed record CalendarEventResponse(
    Guid Id,
    string Title,
    string? Description,
    string Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool AllDay,
    string? Location,
    Guid? ProcessId,
    Guid? AssignedUserId);

public sealed record ListCalendarEventsResponse(IReadOnlyList<CalendarEventResponse> Items);

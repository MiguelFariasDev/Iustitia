using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Workflow.Domain.Calendar;

/// <summary>
/// Compromisso da agenda do escritório: prazo, audiência, reunião ou tarefa. O processo é
/// referenciado por id (módulo Legal), nunca contido — Workflow não conhece o domínio jurídico.
///
/// Lembretes: cada antecedência (1 dia, 1 hora) é registrada ao ser enviada
/// (<see cref="MarkReminderSent"/>) para que o job nunca avise duas vezes. Mudar o horário
/// (<see cref="Reschedule"/>) zera os lembretes, porque o aviso antigo se refere a outro horário.
/// Nenhum evento de domínio é levantado: ainda não há consumidor.
/// </summary>
public sealed class CalendarEvent : AggregateRoot<CalendarEventId>, IAuditable, ISoftDeletable, IHasTenant
{
    public const int TitleMaxLength = 200;

    public Guid TenantId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CalendarEventType Type { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public bool AllDay { get; private set; }

    public string? Location { get; private set; }

    public Guid? ProcessId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public DateTimeOffset? OneDayReminderSentAt { get; private set; }

    public DateTimeOffset? OneHourReminderSentAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private CalendarEvent()
    {
        // EF Core.
    }

    private CalendarEvent(CalendarEventId id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    public static Result<CalendarEvent> Create(
        Guid tenantId,
        string? title,
        string? description,
        CalendarEventType type,
        DateTimeOffset startAt,
        DateTimeOffset? endAt,
        bool allDay,
        string? location,
        Guid? processId,
        Guid? assignedUserId)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        var calendarEvent = new CalendarEvent(CalendarEventId.New(), tenantId);
        var applied = calendarEvent.Apply(title, description, type, startAt, endAt, allDay, location, processId, assignedUserId);

        return applied.IsFailure ? Result.Failure<CalendarEvent>(applied.Error) : Result.Success(calendarEvent);
    }

    public Result Update(
        string? title,
        string? description,
        CalendarEventType type,
        DateTimeOffset startAt,
        DateTimeOffset? endAt,
        bool allDay,
        string? location,
        Guid? processId,
        Guid? assignedUserId)
    {
        var previousStart = StartAt;
        var applied = Apply(title, description, type, startAt, endAt, allDay, location, processId, assignedUserId);

        if (applied.IsSuccess && StartAt != previousStart)
        {
            ResetReminders();
        }

        return applied;
    }

    /// <summary>Muda só o período (arrastar e soltar na agenda). Sem <paramref name="endAt"/>, preserva a duração.</summary>
    public Result Reschedule(DateTimeOffset startAt, DateTimeOffset? endAt, bool? allDay)
    {
        var duration = EndAt - StartAt;
        var newEnd = endAt ?? startAt + duration;

        if (newEnd < startAt)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_INVALID_PERIOD));
        }

        if (startAt != StartAt)
        {
            ResetReminders();
        }

        StartAt = startAt;
        EndAt = newEnd;
        AllDay = allDay ?? AllDay;

        return Result.Success();
    }

    public void MarkReminderSent(ReminderKind kind, DateTimeOffset sentAt)
    {
        if (kind == ReminderKind.OneDayBefore)
        {
            OneDayReminderSentAt = sentAt;
        }
        else
        {
            OneHourReminderSentAt = sentAt;
        }
    }

    private void ResetReminders()
    {
        OneDayReminderSentAt = null;
        OneHourReminderSentAt = null;
    }

    private Result Apply(
        string? title,
        string? description,
        CalendarEventType type,
        DateTimeOffset startAt,
        DateTimeOffset? endAt,
        bool allDay,
        string? location,
        Guid? processId,
        Guid? assignedUserId)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMaxLength)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_TITLE_REQUIRED));
        }

        // Sem término informado, o evento dura 1 hora (ou o dia inteiro, se for de dia inteiro).
        var effectiveEnd = endAt ?? (allDay ? startAt.AddDays(1) : startAt.AddHours(1));
        if (effectiveEnd < startAt)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_INVALID_PERIOD));
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Type = type;
        StartAt = startAt;
        EndAt = effectiveEnd;
        AllDay = allDay;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        ProcessId = processId;
        AssignedUserId = assignedUserId;

        return Result.Success();
    }
}

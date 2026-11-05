using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Workflow.Domain.Agenda;

/// <summary>
/// Compromisso com horário (audiência, reunião…) de um usuário. Diferente de <c>WorkTask</c>, tem
/// hora: é um instante (<see cref="StartsAt"/>), e o dia que ele ocupa na agenda é decidido no fuso
/// do escritório (ver BusinessCalendar).
/// </summary>
public sealed class CalendarEvent : AggregateRoot<CalendarEventId>, IHasTenant
{
    public const int TitleMaxLength = 200;
    public const int LocationMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid TenantId { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public CalendarEventType Type { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public string? Location { get; private set; }

    public Guid? ProcessoId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private CalendarEvent()
    {
        // EF Core.
    }

    private CalendarEvent(
        CalendarEventId id, Guid tenantId, Guid ownerUserId, string title, string? description,
        CalendarEventType type, DateTimeOffset startsAt, DateTimeOffset? endsAt, string? location,
        Guid? processoId, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Title = title;
        Description = description;
        Type = type;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Location = location;
        ProcessoId = processoId;
        CreatedAt = createdAt;
    }

    public static Result<CalendarEvent> Create(
        Guid tenantId,
        Guid ownerUserId,
        string? title,
        string? description,
        CalendarEventType type,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        string? location,
        Guid? processoId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(OwnerUserId)));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(Title)));
        }

        if (endsAt is { } end && end < startsAt)
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.AGENDA_EVENT_PERIOD_INVALID));
        }

        var trimmedTitle = title.Trim();
        var trimmedLocation = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (trimmedTitle.Length > TitleMaxLength)
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(Title), TitleMaxLength));
        }

        if (trimmedLocation is { Length: > LocationMaxLength })
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(Location), LocationMaxLength));
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            return Result.Failure<CalendarEvent>(ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(Description), DescriptionMaxLength));
        }

        return Result.Success(new CalendarEvent(
            CalendarEventId.New(), tenantId, ownerUserId, trimmedTitle, trimmedDescription, type,
            startsAt.ToUniversalTime(), endsAt?.ToUniversalTime(), trimmedLocation,
            processoId == Guid.Empty ? null : processoId, now.ToUniversalTime()));
    }
}

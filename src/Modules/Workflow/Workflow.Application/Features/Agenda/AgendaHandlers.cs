using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Agenda;
using FluentValidation;

namespace Advocacia.Modules.Workflow.Application.Features.Agenda;

public sealed class GetAgendaHandler(
    IWorkTaskRepository tasks, ICalendarEventRepository events, ICurrentUser currentUser, BusinessCalendar calendar)
    : IQueryHandler<GetAgendaQuery, AgendaResponse>
{
    /// <summary>Dois meses de calendário: o que o app pede ao mostrar um mês (com os dias vizinhos).</summary>
    public const int MaxDays = 62;

    public async Task<Result<AgendaResponse>> Handle(GetAgendaQuery request, CancellationToken cancellationToken)
    {
        if (!WorkflowScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<AgendaResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Teto no tamanho do intervalo: sem ele, uma chamada com 20 anos de datas traria a agenda
        // inteira do usuário de uma vez.
        if (request.End < request.Start || request.End.DayNumber - request.Start.DayNumber + 1 > MaxDays)
        {
            return Result.Failure<AgendaResponse>(ErrorFactory.From(ErrorCode.AGENDA_PERIOD_INVALID, MaxDays));
        }

        var dueTasks = await tasks.ListDueBetweenAsync(tenantId, userId, request.Start, request.End, cancellationToken);
        var calendarEvents = await events.ListStartingBetweenAsync(
            tenantId, userId, calendar.StartOfDayUtc(request.Start), calendar.StartOfDayUtc(request.End.AddDays(1)), cancellationToken);

        var items = dueTasks.Select(AgendaMapping.From)
            .Concat(calendarEvents.Select(e => AgendaMapping.From(e, calendar)))
            // Por dia; no dia, prazos (sem horário) antes dos eventos, e estes por horário.
            .OrderBy(item => item.Date)
            .ThenBy(item => item.AllDay ? 0 : 1)
            .ThenBy(item => item.StartsAt)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Result.Success(new AgendaResponse(request.Start, request.End, items));
    }
}

public sealed class CreateCalendarEventValidator : AbstractValidator<CreateCalendarEventCommand>
{
    public CreateCalendarEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(CalendarEvent.TitleMaxLength);
        RuleFor(x => x.Location).MaximumLength(CalendarEvent.LocationMaxLength);
        RuleFor(x => x.Description).MaximumLength(CalendarEvent.DescriptionMaxLength);
        RuleFor(x => x.Type)
            .Must(value => Enum.TryParse<CalendarEventType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .WithErrorCode(nameof(ErrorCode.VALIDATION_INVALID_ENUM))
            .WithMessage("Tipo inválido. Use Hearing, Meeting ou Other.");
    }
}

public sealed class CreateCalendarEventHandler(
    ICalendarEventRepository events, IWorkflowUnitOfWork unitOfWork, ICurrentUser currentUser,
    IDateTimeProvider clock, BusinessCalendar calendar)
    : ICommandHandler<CreateCalendarEventCommand, AgendaItemResponse>
{
    public async Task<Result<AgendaItemResponse>> Handle(CreateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        if (!WorkflowScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<AgendaItemResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Enum.TryParse<CalendarEventType>(request.Type, ignoreCase: true, out var type) || !Enum.IsDefined(type))
        {
            return Result.Failure<AgendaItemResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
        }

        var created = CalendarEvent.Create(
            tenantId, userId, request.Title, request.Description, type, request.StartsAt, request.EndsAt,
            request.Location, request.ProcessoId, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<AgendaItemResponse>(created.Error);
        }

        events.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AgendaMapping.From(created.Value, calendar));
    }
}

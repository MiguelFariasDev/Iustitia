using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Calendar;

namespace Advocacia.Modules.Workflow.Application.Features.Calendar;

/// <summary>O tenant vem SEMPRE do JWT (ICurrentUser), nunca do corpo da requisição.</summary>
public sealed class CreateCalendarEventHandler(
    ICalendarEventRepository repository,
    IProcessLookup processLookup,
    IWorkflowUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateCalendarEventCommand, CalendarEventResponse>
{
    public async Task<Result<CalendarEventResponse>> Handle(
        CreateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<CalendarEventResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Enum.TryParse<CalendarEventType>(request.Type, ignoreCase: true, out var type))
        {
            return Result.Failure<CalendarEventResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
        }

        if (request.ProcessId is { } processId
            && !await processLookup.ExistsAsync(tenantId, processId, cancellationToken))
        {
            return Result.Failure<CalendarEventResponse>(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_PROCESS_NOT_FOUND));
        }

        // Sem responsável informado, o criador é o responsável: é ele quem recebe os lembretes.
        var calendarEvent = CalendarEvent.Create(
            tenantId, request.Title, request.Description, type, request.StartAt, request.EndAt,
            request.AllDay, request.Location, request.ProcessId, request.AssignedUserId ?? currentUser.UserId);

        if (calendarEvent.IsFailure)
        {
            return Result.Failure<CalendarEventResponse>(calendarEvent.Error);
        }

        repository.Add(calendarEvent.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(calendarEvent.Value.ToResponse());
    }
}

public sealed class UpdateCalendarEventHandler(
    ICalendarEventRepository repository,
    IProcessLookup processLookup,
    IWorkflowUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateCalendarEventCommand>
{
    public async Task<Result> Handle(UpdateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await repository.GetByIdAsync(CalendarEventId.From(request.EventId), cancellationToken);
        if (calendarEvent is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_NOT_FOUND));
        }

        if (!Enum.TryParse<CalendarEventType>(request.Type, ignoreCase: true, out var type))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
        }

        if (request.ProcessId is { } processId
            && currentUser.TenantId is { } tenantId
            && !await processLookup.ExistsAsync(tenantId, processId, cancellationToken))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_PROCESS_NOT_FOUND));
        }

        var result = calendarEvent.Update(
            request.Title, request.Description, type, request.StartAt, request.EndAt, request.AllDay,
            request.Location, request.ProcessId, request.AssignedUserId);
        if (result.IsFailure)
        {
            return result;
        }

        repository.Update(calendarEvent);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class RescheduleCalendarEventHandler(
    ICalendarEventRepository repository,
    IWorkflowUnitOfWork unitOfWork) : ICommandHandler<RescheduleCalendarEventCommand>
{
    public async Task<Result> Handle(RescheduleCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await repository.GetByIdAsync(CalendarEventId.From(request.EventId), cancellationToken);
        if (calendarEvent is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_NOT_FOUND));
        }

        var result = calendarEvent.Reschedule(request.StartAt, request.EndAt, request.AllDay);
        if (result.IsFailure)
        {
            return result;
        }

        repository.Update(calendarEvent);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Exclusão lógica (o AuditInterceptor converte Remove em soft delete).</summary>
public sealed class DeleteCalendarEventHandler(
    ICalendarEventRepository repository,
    IWorkflowUnitOfWork unitOfWork) : ICommandHandler<DeleteCalendarEventCommand>
{
    public async Task<Result> Handle(DeleteCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await repository.GetByIdAsync(CalendarEventId.From(request.EventId), cancellationToken);
        if (calendarEvent is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_NOT_FOUND));
        }

        repository.Remove(calendarEvent);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class GetCalendarEventByIdHandler(ICalendarEventRepository repository)
    : IQueryHandler<GetCalendarEventByIdQuery, CalendarEventResponse>
{
    public async Task<Result<CalendarEventResponse>> Handle(
        GetCalendarEventByIdQuery request, CancellationToken cancellationToken)
    {
        var calendarEvent = await repository.GetByIdAsync(CalendarEventId.From(request.EventId), cancellationToken);

        return calendarEvent is null
            ? Result.Failure<CalendarEventResponse>(ErrorFactory.From(ErrorCode.CALENDAR_EVENT_NOT_FOUND))
            : Result.Success(calendarEvent.ToResponse());
    }
}

public sealed class ListCalendarEventsHandler(ICalendarEventRepository repository, ICurrentUser currentUser)
    : IQueryHandler<ListCalendarEventsQuery, ListCalendarEventsResponse>
{
    public async Task<Result<ListCalendarEventsResponse>> Handle(
        ListCalendarEventsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListCalendarEventsResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        CalendarEventType? type = null;
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            if (!Enum.TryParse<CalendarEventType>(request.Type, ignoreCase: true, out var parsed))
            {
                return Result.Failure<ListCalendarEventsResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Type)));
            }

            type = parsed;
        }

        var items = await repository.ListByPeriodAsync(
            tenantId, request.From, request.To, type, request.AssignedUserId, request.ProcessId, cancellationToken);

        return Result.Success(new ListCalendarEventsResponse(items.Select(item => item.ToResponse()).ToList()));
    }
}

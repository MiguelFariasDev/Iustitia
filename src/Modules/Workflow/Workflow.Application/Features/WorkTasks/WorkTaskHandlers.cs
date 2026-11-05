using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.WorkTasks;
using FluentValidation;

namespace Advocacia.Modules.Workflow.Application.Features.WorkTasks;

internal static class TaskFilters
{
    internal const string AcceptedFilters = "all, today, overdue, next_7_days";
    internal const string AcceptedStatuses = "Pending, Completed";

    internal static bool TryParseFilter(string? value, out TaskDueFilter filter)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "all":
                filter = TaskDueFilter.All;
                return true;
            case "today":
                filter = TaskDueFilter.Today;
                return true;
            case "overdue":
                filter = TaskDueFilter.Overdue;
                return true;
            case "next_7_days":
                filter = TaskDueFilter.Next7Days;
                return true;
            default:
                filter = TaskDueFilter.All;
                return false;
        }
    }

    internal static bool TryParseStatus(string? value, out WorkTaskStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        // IsDefined: "5" é parseável como número, mas não é um status.
        if (Enum.TryParse<WorkTaskStatus>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            status = parsed;
            return true;
        }

        return false;
    }
}

public sealed class ListWorkTasksHandler(IWorkTaskRepository tasks, ICurrentUser currentUser, BusinessCalendar calendar)
    : IQueryHandler<ListWorkTasksQuery, WorkTasksPageResponse>
{
    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    public async Task<Result<WorkTasksPageResponse>> Handle(ListWorkTasksQuery request, CancellationToken cancellationToken)
    {
        if (!WorkflowScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<WorkTasksPageResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!TaskFilters.TryParseFilter(request.Filter, out var filter))
        {
            return Result.Failure<WorkTasksPageResponse>(ErrorFactory.From(ErrorCode.TASK_FILTER_INVALID, TaskFilters.AcceptedFilters));
        }

        if (!TaskFilters.TryParseStatus(request.Status, out var status))
        {
            return Result.Failure<WorkTasksPageResponse>(ErrorFactory.From(ErrorCode.TASK_FILTER_INVALID, TaskFilters.AcceptedStatuses));
        }

        var today = calendar.Today;
        var query = new WorkTaskQuery(status, filter, today);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;

        var items = await tasks.ListAsync(tenantId, userId, query, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await tasks.CountAsync(tenantId, userId, query, cancellationToken);

        return Result.Success(new WorkTasksPageResponse(
            items.Select(task => WorkTaskResponse.From(task, today)).ToList(),
            page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }
}

public sealed class GetWorkTasksSummaryHandler(IWorkTaskRepository tasks, ICurrentUser currentUser, BusinessCalendar calendar)
    : IQueryHandler<GetWorkTasksSummaryQuery, WorkTasksSummaryResponse>
{
    public async Task<Result<WorkTasksSummaryResponse>> Handle(GetWorkTasksSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!WorkflowScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<WorkTasksSummaryResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var today = calendar.Today;
        var pending = WorkTaskStatus.Pending;

        // Sequencial de propósito: um DbContext não aceita consultas concorrentes.
        var totalPending = await tasks.CountAsync(tenantId, userId, new WorkTaskQuery(pending, TaskDueFilter.All, today), cancellationToken);
        var dueToday = await tasks.CountAsync(tenantId, userId, new WorkTaskQuery(pending, TaskDueFilter.Today, today), cancellationToken);
        var overdue = await tasks.CountAsync(tenantId, userId, new WorkTaskQuery(pending, TaskDueFilter.Overdue, today), cancellationToken);
        var next7 = await tasks.CountAsync(tenantId, userId, new WorkTaskQuery(pending, TaskDueFilter.Next7Days, today), cancellationToken);
        var completedThisMonth = await tasks.CountCompletedSinceAsync(
            tenantId, userId, calendar.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1)), cancellationToken);

        return Result.Success(new WorkTasksSummaryResponse(totalPending, dueToday, overdue, next7, completedThisMonth));
    }
}

public sealed class CreateWorkTaskValidator : AbstractValidator<CreateWorkTaskCommand>
{
    public CreateWorkTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(WorkTask.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(WorkTask.DescriptionMaxLength);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(WorkTask.MinimumDueDate)
            .WithErrorCode(nameof(ErrorCode.TASK_DUE_DATE_INVALID));
    }
}

/// <summary>A tarefa é do usuário que a cria. Atribuir a outra pessoa fica para quando houver o fluxo de equipe.</summary>
public sealed class CreateWorkTaskHandler(
    IWorkTaskRepository tasks, IWorkflowUnitOfWork unitOfWork, ICurrentUser currentUser, IDateTimeProvider clock, BusinessCalendar calendar)
    : ICommandHandler<CreateWorkTaskCommand, WorkTaskResponse>
{
    public async Task<Result<WorkTaskResponse>> Handle(CreateWorkTaskCommand request, CancellationToken cancellationToken)
    {
        if (!WorkflowScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure<WorkTaskResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var created = WorkTask.Create(
            tenantId, userId, request.Title, request.Description, request.DueDate, request.ProcessoId, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<WorkTaskResponse>(created.Error);
        }

        tasks.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(WorkTaskResponse.From(created.Value, calendar.Today));
    }
}

public sealed class CompleteWorkTaskHandler(
    IWorkTaskRepository tasks, IWorkflowUnitOfWork unitOfWork, ICurrentUser currentUser, IDateTimeProvider clock)
    : ICommandHandler<CompleteWorkTaskCommand>
{
    public async Task<Result> Handle(CompleteWorkTaskCommand request, CancellationToken cancellationToken)
    {
        if (!WorkflowScope.TryResolve(currentUser, out var tenantId, out var userId))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        // Tarefa de outro usuário "não existe" para este (404, sem revelar que o id existe).
        var task = await tasks.GetForAssigneeAsync(tenantId, userId, WorkTaskId.From(request.TaskId), cancellationToken);
        if (task is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TASK_NOT_FOUND));
        }

        var result = task.Complete(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        tasks.Update(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

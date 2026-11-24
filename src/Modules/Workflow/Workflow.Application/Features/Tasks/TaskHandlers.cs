using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Tasks;

namespace Advocacia.Modules.Workflow.Application.Features.Tasks;

/// <summary>O tenant vem SEMPRE do JWT (ICurrentUser), nunca do corpo da requisição.</summary>
public sealed class CreateWorkTaskHandler(
    IWorkTaskRepository repository,
    IProcessLookup processLookup,
    IWorkflowUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateWorkTaskCommand, WorkTaskResponse>
{
    public async Task<Result<WorkTaskResponse>> Handle(CreateWorkTaskCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<WorkTaskResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (request.ProcessId is { } processId
            && !await processLookup.ExistsAsync(tenantId, processId, cancellationToken))
        {
            return Result.Failure<WorkTaskResponse>(ErrorFactory.From(ErrorCode.WORK_TASK_PROCESS_NOT_FOUND));
        }

        var task = WorkTask.Create(
            tenantId, request.Title, request.Description, request.DueDate, request.ProcessId, request.AssignedUserId);
        if (task.IsFailure)
        {
            return Result.Failure<WorkTaskResponse>(task.Error);
        }

        repository.Add(task.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(task.Value.ToResponse());
    }
}

public sealed class UpdateWorkTaskHandler(
    IWorkTaskRepository repository,
    IProcessLookup processLookup,
    IWorkflowUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateWorkTaskCommand>
{
    public async Task<Result> Handle(UpdateWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(WorkTaskId.From(request.TaskId), cancellationToken);
        if (task is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.WORK_TASK_NOT_FOUND));
        }

        if (request.ProcessId is { } processId
            && currentUser.TenantId is { } tenantId
            && !await processLookup.ExistsAsync(tenantId, processId, cancellationToken))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.WORK_TASK_PROCESS_NOT_FOUND));
        }

        var result = task.Update(request.Title, request.Description, request.DueDate, request.ProcessId, request.AssignedUserId);
        if (result.IsFailure)
        {
            return result;
        }

        repository.Update(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ChangeWorkTaskStatusHandler(
    IWorkTaskRepository repository,
    IWorkflowUnitOfWork unitOfWork) : ICommandHandler<ChangeWorkTaskStatusCommand>
{
    public async Task<Result> Handle(ChangeWorkTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(WorkTaskId.From(request.TaskId), cancellationToken);
        if (task is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.WORK_TASK_NOT_FOUND));
        }

        if (!Enum.TryParse<WorkTaskStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Status)));
        }

        task.ChangeStatus(status);
        repository.Update(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Exclusão lógica (o AuditInterceptor converte Remove em soft delete).</summary>
public sealed class DeleteWorkTaskHandler(
    IWorkTaskRepository repository,
    IWorkflowUnitOfWork unitOfWork) : ICommandHandler<DeleteWorkTaskCommand>
{
    public async Task<Result> Handle(DeleteWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(WorkTaskId.From(request.TaskId), cancellationToken);
        if (task is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.WORK_TASK_NOT_FOUND));
        }

        repository.Remove(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ListWorkTasksHandler(IWorkTaskRepository repository, ICurrentUser currentUser)
    : IQueryHandler<ListWorkTasksQuery, ListWorkTasksResponse>
{
    public async Task<Result<ListWorkTasksResponse>> Handle(ListWorkTasksQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListWorkTasksResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var items = await repository.ListAsync(tenantId, request.ProcessId, request.AssignedUserId, cancellationToken);

        return Result.Success(new ListWorkTasksResponse(items.Select(item => item.ToResponse()).ToList()));
    }
}

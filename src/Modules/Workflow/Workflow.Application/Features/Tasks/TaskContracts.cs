using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Workflow.Application.Features.Tasks;

public sealed record CreateWorkTaskCommand(
    string Title,
    DateOnly? DueDate = null,
    string? Description = null,
    Guid? ProcessId = null,
    Guid? AssignedUserId = null) : ICommand<WorkTaskResponse>;

public sealed record UpdateWorkTaskCommand(
    Guid TaskId,
    string Title,
    DateOnly? DueDate = null,
    string? Description = null,
    Guid? ProcessId = null,
    Guid? AssignedUserId = null) : ICommand;

/// <summary><c>Status</c> é "Todo", "InProgress", "InReview" ou "Done".</summary>
public sealed record ChangeWorkTaskStatusCommand(Guid TaskId, string Status) : ICommand;

public sealed record DeleteWorkTaskCommand(Guid TaskId) : ICommand;

public sealed record ListWorkTasksQuery(Guid? ProcessId = null, Guid? AssignedUserId = null)
    : IQuery<ListWorkTasksResponse>;

public sealed record WorkTaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateOnly? DueDate,
    Guid? ProcessId,
    Guid? AssignedUserId,
    DateTimeOffset CreatedAt);

public sealed record ListWorkTasksResponse(IReadOnlyList<WorkTaskResponse> Items);

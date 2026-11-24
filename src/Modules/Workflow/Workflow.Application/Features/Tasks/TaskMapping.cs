using Advocacia.Modules.Workflow.Domain.Tasks;

namespace Advocacia.Modules.Workflow.Application.Features.Tasks;

internal static class TaskMapping
{
    internal static WorkTaskResponse ToResponse(this WorkTask task) => new(
        task.Id.Value,
        task.Title,
        task.Description,
        task.Status.ToString(),
        task.DueDate,
        task.ProcessId,
        task.AssignedUserId,
        task.CreatedAt);
}

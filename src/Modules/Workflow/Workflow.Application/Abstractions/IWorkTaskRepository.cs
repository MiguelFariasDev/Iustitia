using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Tasks;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

public interface IWorkTaskRepository : IRepository<WorkTask, WorkTaskId>
{
    Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid tenantId, Guid? processId, Guid? assignedUserId, CancellationToken cancellationToken = default);
}

using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;

public sealed class WorkTaskRepository(WorkflowDbContext dbContext)
    : Repository<WorkTask, WorkTaskId>(dbContext), IWorkTaskRepository
{
    public async Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid tenantId, Guid? processId, Guid? assignedUserId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(task =>
                task.TenantId == tenantId
                && (processId == null || task.ProcessId == processId)
                && (assignedUserId == null || task.AssignedUserId == assignedUserId))
            .OrderBy(task => task.CreatedAt)
            .ToListAsync(cancellationToken);
}

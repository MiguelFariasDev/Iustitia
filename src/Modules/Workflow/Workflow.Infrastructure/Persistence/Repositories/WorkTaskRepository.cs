using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.WorkTasks;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Repositories;

/// <summary>Toda consulta repete tenant E assignee no Where, além do filtro global e do RLS: tarefa é pessoal.</summary>
public sealed class WorkTaskRepository(WorkflowDbContext dbContext)
    : Repository<WorkTask, WorkTaskId>(dbContext), IWorkTaskRepository
{
    public async Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid tenantId, Guid assigneeId, WorkTaskQuery query, int skip, int take, CancellationToken cancellationToken = default) =>
        await Filter(tenantId, assigneeId, query)
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.Title)
            .ThenBy(task => task.Id) // desempate estável: a paginação não pode repetir nem pular itens
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(Guid tenantId, Guid assigneeId, WorkTaskQuery query, CancellationToken cancellationToken = default) =>
        Filter(tenantId, assigneeId, query).CountAsync(cancellationToken);

    public Task<int> CountCompletedSinceAsync(
        Guid tenantId, Guid assigneeId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        DbSet.CountAsync(
            task => task.TenantId == tenantId && task.AssigneeUserId == assigneeId
                && task.Status == WorkTaskStatus.Completed && task.CompletedAt >= sinceUtc,
            cancellationToken);

    public Task<WorkTask?> GetForAssigneeAsync(
        Guid tenantId, Guid assigneeId, WorkTaskId id, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            task => task.Id == id && task.TenantId == tenantId && task.AssigneeUserId == assigneeId, cancellationToken);

    public async Task<IReadOnlyList<WorkTask>> ListDueBetweenAsync(
        Guid tenantId, Guid assigneeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(task => task.TenantId == tenantId && task.AssigneeUserId == assigneeId
                && task.DueDate >= from && task.DueDate <= to)
            .ToListAsync(cancellationToken);

    private IQueryable<WorkTask> Filter(Guid tenantId, Guid assigneeId, WorkTaskQuery query)
    {
        var tasks = DbSet.Where(task => task.TenantId == tenantId && task.AssigneeUserId == assigneeId);

        if (query.Status is { } status)
        {
            tasks = tasks.Where(task => task.Status == status);
        }

        var today = query.Today;
        var inSevenDays = today.AddDays(7);

        return query.Filter switch
        {
            TaskDueFilter.Today => tasks.Where(task => task.DueDate == today),
            // Concluída nunca está "atrasada", mesmo que o status pedido seja Completed.
            TaskDueFilter.Overdue => tasks.Where(task => task.DueDate < today && task.Status == WorkTaskStatus.Pending),
            TaskDueFilter.Next7Days => tasks.Where(task => task.DueDate >= today && task.DueDate <= inSevenDays),
            _ => tasks,
        };
    }
}

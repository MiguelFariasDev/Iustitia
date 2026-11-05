using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.WorkTasks;

namespace Advocacia.Modules.Workflow.Application.Abstractions;

/// <summary>
/// Consultas de tarefas. Todas são do ASSIGNEE (tenant E usuário): tarefa é pessoal, e o filtro por
/// tenant sozinho deixaria um colega ler os prazos do outro.
/// </summary>
public interface IWorkTaskRepository : IRepository<WorkTask, WorkTaskId>
{
    Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid tenantId, Guid assigneeId, WorkTaskQuery query, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid tenantId, Guid assigneeId, WorkTaskQuery query, CancellationToken cancellationToken = default);

    Task<WorkTask?> GetForAssigneeAsync(Guid tenantId, Guid assigneeId, WorkTaskId id, CancellationToken cancellationToken = default);

    /// <summary>Quantas tarefas do usuário foram concluídas a partir de <paramref name="sinceUtc"/> (inclusive).</summary>
    Task<int> CountCompletedSinceAsync(Guid tenantId, Guid assigneeId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Tarefas (de qualquer status) com vencimento no intervalo, ambos inclusivos — alimenta a agenda.</summary>
    Task<IReadOnlyList<WorkTask>> ListDueBetweenAsync(
        Guid tenantId, Guid assigneeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <param name="Status">Só pendentes, só concluídas, ou ambas (null).</param>
/// <param name="Filter">Recorte por vencimento.</param>
/// <param name="Today">"Hoje" no fuso do escritório (ver BusinessCalendar).</param>
public sealed record WorkTaskQuery(WorkTaskStatus? Status, TaskDueFilter Filter, DateOnly Today);

public enum TaskDueFilter
{
    All,

    /// <summary>Vence hoje.</summary>
    Today,

    /// <summary>Venceu antes de hoje E ainda está pendente (concluída nunca está "atrasada").</summary>
    Overdue,

    /// <summary>Vence de hoje até hoje + 7 dias, inclusive.</summary>
    Next7Days,
}

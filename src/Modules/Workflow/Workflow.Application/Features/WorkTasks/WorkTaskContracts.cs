using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.Modules.Workflow.Domain.WorkTasks;

namespace Advocacia.Modules.Workflow.Application.Features.WorkTasks;

/// <param name="Id">Id da tarefa.</param>
/// <param name="Title">Título.</param>
/// <param name="Description">Detalhe opcional.</param>
/// <param name="DueDate">Data de vencimento (AAAA-MM-DD), sem horário.</param>
/// <param name="ProcessoId">Processo relacionado, se houver (só referência).</param>
/// <param name="Status"><c>Pending</c> ou <c>Completed</c>.</param>
/// <param name="CompletedAt">Quando foi concluída.</param>
/// <param name="IsOverdue">Pendente E com vencimento antes de hoje (no fuso do escritório).</param>
/// <param name="CreatedAt">Criação.</param>
public sealed record WorkTaskResponse(
    Guid Id,
    string Title,
    string? Description,
    DateOnly DueDate,
    Guid? ProcessoId,
    string Status,
    DateTimeOffset? CompletedAt,
    bool IsOverdue,
    DateTimeOffset CreatedAt)
{
    public static WorkTaskResponse From(WorkTask task, DateOnly today) => new(
        task.Id.Value,
        task.Title,
        task.Description,
        task.DueDate,
        task.ProcessoId,
        task.Status.ToString(),
        task.CompletedAt,
        task.Status == WorkTaskStatus.Pending && task.DueDate < today,
        task.CreatedAt);
}

public sealed record WorkTasksPageResponse(
    IReadOnlyList<WorkTaskResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <param name="Pending">Todas as pendentes.</param>
/// <param name="DueToday">Pendentes que vencem hoje.</param>
/// <param name="Overdue">Pendentes que venceram antes de hoje.</param>
/// <param name="Next7Days">Pendentes que vencem de hoje a hoje + 7 dias (inclusive).</param>
/// <param name="CompletedThisMonth">Concluídas neste mês (no fuso do escritório).</param>
public sealed record WorkTasksSummaryResponse(int Pending, int DueToday, int Overdue, int Next7Days, int CompletedThisMonth);

/// <param name="Status"><c>Pending</c> ou <c>Completed</c>; vazio = ambos.</param>
/// <param name="Filter"><c>all</c>, <c>today</c>, <c>overdue</c> ou <c>next_7_days</c>; vazio = all.</param>
/// <param name="Page">Página (1-based).</param>
/// <param name="PageSize">Itens por página (máx. 200).</param>
public sealed record ListWorkTasksQuery(string? Status = null, string? Filter = null, int Page = 1, int PageSize = 50)
    : IQuery<WorkTasksPageResponse>;

public sealed record GetWorkTasksSummaryQuery : IQuery<WorkTasksSummaryResponse>;

/// <param name="Title">Obrigatório, até 200 caracteres.</param>
/// <param name="Description">Opcional, até 2000.</param>
/// <param name="DueDate">Data de vencimento (AAAA-MM-DD).</param>
/// <param name="ProcessoId">Processo relacionado (opcional).</param>
public sealed record CreateWorkTaskCommand(string Title, string? Description, DateOnly DueDate, Guid? ProcessoId)
    : ICommand<WorkTaskResponse>;

public sealed record CompleteWorkTaskCommand(Guid TaskId) : ICommand;

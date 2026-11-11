using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Workflow.Domain.Tasks;

/// <summary>
/// Tarefa do escritório (o que falta fazer), opcionalmente ligada a um processo e a um
/// responsável. Diferente de <c>CalendarEvent</c>: não ocupa horário na agenda, só tem um prazo
/// (dia) e uma situação no quadro. O processo é referenciado por id (módulo Legal).
/// </summary>
public sealed class WorkTask : AggregateRoot<WorkTaskId>, IAuditable, ISoftDeletable, IHasTenant
{
    public const int TitleMaxLength = 200;

    public Guid TenantId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public WorkTaskStatus Status { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? ProcessId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private WorkTask()
    {
        // EF Core.
    }

    private WorkTask(WorkTaskId id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    public static Result<WorkTask> Create(
        Guid tenantId, string? title, string? description, DateOnly? dueDate, Guid? processId, Guid? assignedUserId)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        var task = new WorkTask(WorkTaskId.New(), tenantId);
        var applied = task.Apply(title, description, dueDate, processId, assignedUserId);

        return applied.IsFailure ? Result.Failure<WorkTask>(applied.Error) : Result.Success(task);
    }

    public Result Update(string? title, string? description, DateOnly? dueDate, Guid? processId, Guid? assignedUserId) =>
        Apply(title, description, dueDate, processId, assignedUserId);

    public void ChangeStatus(WorkTaskStatus status) => Status = status;

    private Result Apply(string? title, string? description, DateOnly? dueDate, Guid? processId, Guid? assignedUserId)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMaxLength)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.WORK_TASK_TITLE_REQUIRED));
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        ProcessId = processId;
        AssignedUserId = assignedUserId;

        return Result.Success();
    }
}

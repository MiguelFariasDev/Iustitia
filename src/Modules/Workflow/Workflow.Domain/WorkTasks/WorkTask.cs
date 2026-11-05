using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Workflow.Domain.WorkTasks;

/// <summary>
/// Tarefa ou prazo de um usuário. O nome é <c>WorkTask</c> para não colidir com
/// <c>System.Threading.Tasks.Task</c>.
///
/// <b>O vencimento é uma DATA</b> (<see cref="DateOnly"/>), não um instante: prazo jurídico vence
/// "no dia 12/11", e "vence hoje" depende do dia no fuso do escritório. Guardar um instante em UTC
/// faria um prazo de 12/11 à noite, em São Paulo, parecer do dia 13.
///
/// <see cref="ProcessoId"/> é só uma referência: o Workflow não conhece o módulo Legal (ADR-001),
/// então não há chave estrangeira nem validação de que o processo existe.
/// </summary>
public sealed class WorkTask : AggregateRoot<WorkTaskId>, IHasTenant
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    /// <summary>Datas anteriores a isto são quase certamente um valor padrão (0001-01-01) esquecido.</summary>
    public static readonly DateOnly MinimumDueDate = new(2000, 1, 1);

    public Guid TenantId { get; private set; }

    public Guid AssigneeUserId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateOnly DueDate { get; private set; }

    public Guid? ProcessoId { get; private set; }

    public WorkTaskStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    private WorkTask()
    {
        // EF Core.
    }

    private WorkTask(
        WorkTaskId id, Guid tenantId, Guid assigneeUserId, string title, string? description,
        DateOnly dueDate, Guid? processoId, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        AssigneeUserId = assigneeUserId;
        Title = title;
        Description = description;
        DueDate = dueDate;
        ProcessoId = processoId;
        Status = WorkTaskStatus.Pending;
        CreatedAt = createdAt;
    }

    public static Result<WorkTask> Create(
        Guid tenantId,
        Guid assigneeUserId,
        string? title,
        string? description,
        DateOnly dueDate,
        Guid? processoId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (assigneeUserId == Guid.Empty)
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(AssigneeUserId)));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(Title)));
        }

        if (dueDate < MinimumDueDate)
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.TASK_DUE_DATE_INVALID));
        }

        var trimmedTitle = title.Trim();
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (trimmedTitle.Length > TitleMaxLength)
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(Title), TitleMaxLength));
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            return Result.Failure<WorkTask>(ErrorFactory.From(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED, nameof(Description), DescriptionMaxLength));
        }

        return Result.Success(new WorkTask(
            WorkTaskId.New(), tenantId, assigneeUserId, trimmedTitle, trimmedDescription,
            dueDate, processoId == Guid.Empty ? null : processoId, now.ToUniversalTime()));
    }

    public Result Complete(DateTimeOffset now)
    {
        if (Status == WorkTaskStatus.Completed)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.TASK_ALREADY_COMPLETED));
        }

        Status = WorkTaskStatus.Completed;
        CompletedAt = now.ToUniversalTime();

        return Result.Success();
    }
}

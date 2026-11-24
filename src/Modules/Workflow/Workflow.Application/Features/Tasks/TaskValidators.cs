using Advocacia.Modules.Workflow.Domain.Tasks;
using FluentValidation;

namespace Advocacia.Modules.Workflow.Application.Features.Tasks;

public sealed class CreateWorkTaskValidator : AbstractValidator<CreateWorkTaskCommand>
{
    public CreateWorkTaskValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(WorkTask.TitleMaxLength);
        RuleFor(command => command.Description).MaximumLength(4000);
    }
}

public sealed class UpdateWorkTaskValidator : AbstractValidator<UpdateWorkTaskCommand>
{
    public UpdateWorkTaskValidator()
    {
        RuleFor(command => command.TaskId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(WorkTask.TitleMaxLength);
        RuleFor(command => command.Description).MaximumLength(4000);
    }
}

public sealed class ChangeWorkTaskStatusValidator : AbstractValidator<ChangeWorkTaskStatusCommand>
{
    public ChangeWorkTaskStatusValidator()
    {
        RuleFor(command => command.TaskId).NotEmpty();
        RuleFor(command => command.Status)
            .NotEmpty()
            .Must(status => Enum.TryParse<WorkTaskStatus>(status, ignoreCase: true, out _))
            .WithMessage("Situação inválida. Valores aceitos: Todo, InProgress, InReview, Done.");
    }
}

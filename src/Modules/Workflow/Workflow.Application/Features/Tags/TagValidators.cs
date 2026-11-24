using Advocacia.Modules.Workflow.Domain.Tags;
using FluentValidation;

namespace Advocacia.Modules.Workflow.Application.Features.Tags;

public sealed class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Tag.NameMaxLength);
    }
}

public sealed class SetTagAssignmentValidator : AbstractValidator<SetTagAssignmentCommand>
{
    public SetTagAssignmentValidator()
    {
        RuleFor(command => command.TagId).NotEmpty();
        RuleFor(command => command.TargetId).NotEmpty();
        RuleFor(command => command.Target)
            .NotEmpty()
            .Must(target => Enum.TryParse<TagTarget>(target, ignoreCase: true, out _))
            .WithMessage("Alvo inválido. Valores aceitos: Processo, Advogado.");
    }
}

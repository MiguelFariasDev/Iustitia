using Advocacia.Modules.Legal.Domain.Publications;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.ChangePublicationStatus;

public sealed class ChangePublicationStatusValidator : AbstractValidator<ChangePublicationStatusCommand>
{
    public ChangePublicationStatusValidator()
    {
        RuleFor(command => command.PublicationId).NotEmpty();

        RuleFor(command => command.Status)
            .NotEmpty()
            .Must(value => Enum.TryParse<PublicationStatus>(value, ignoreCase: true, out _))
            .WithErrorCode("EnumValidator")
            .WithMessage("Status de publicação inválido.");

        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

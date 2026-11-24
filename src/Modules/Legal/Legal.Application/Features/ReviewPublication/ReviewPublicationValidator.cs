using Advocacia.Modules.Legal.Domain.Prazos;
using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.ReviewPublication;

public sealed class ReviewPublicationValidator : AbstractValidator<ReviewPublicationCommand>
{
    public ReviewPublicationValidator()
    {
        RuleFor(command => command.PublicationId).NotEmpty();

        RuleFor(command => command.Decision)
            .NotEmpty()
            .Must(decision => decision is "Approved" or "Corrected" or "Rejected")
            .WithErrorCode("EnumValidator")
            .WithMessage("Decisão inválida. Valores aceitos: Approved, Corrected, Rejected.");

        RuleFor(command => command.DeadlineDays)
            .InclusiveBetween(1, CalculadoraPrazo.MaxDias)
            .When(command => command.DeadlineDays is not null);

        RuleFor(command => command.Notes).MaximumLength(1500);
    }
}

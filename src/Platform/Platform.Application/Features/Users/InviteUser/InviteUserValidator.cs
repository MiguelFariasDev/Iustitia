using Advocacia.Platform.Domain.Identity;
using FluentValidation;

namespace Advocacia.Platform.Application.Features.Users.InviteUser;

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: true, out _))
            .WithMessage("Papel inválido. Valores aceitos: Owner, Partner, Lawyer, Intern, Admin, Financial.");
    }
}

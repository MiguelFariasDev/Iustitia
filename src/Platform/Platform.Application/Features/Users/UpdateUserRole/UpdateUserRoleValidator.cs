using Advocacia.Platform.Domain.Identity;
using FluentValidation;

namespace Advocacia.Platform.Application.Features.Users.UpdateUserRole;

public sealed class UpdateUserRoleValidator : AbstractValidator<UpdateUserRoleCommand>
{
    public UpdateUserRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: true, out _))
            .WithMessage("Papel inválido. Valores aceitos: Owner, Partner, Lawyer, Intern, Admin, Financial.");
    }
}

using FluentValidation;

namespace Advocacia.Platform.Application.Features.Users.DeactivateUser;

public sealed class DeactivateUserValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

using FluentValidation;

namespace Advocacia.Platform.Application.Features.Users.AcceptInvite;

public sealed class AcceptInviteValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

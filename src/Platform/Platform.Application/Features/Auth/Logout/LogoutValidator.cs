using FluentValidation;

namespace Advocacia.Platform.Application.Features.Auth.Logout;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
    }
}

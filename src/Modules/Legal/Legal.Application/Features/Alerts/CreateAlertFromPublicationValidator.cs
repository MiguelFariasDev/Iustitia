using FluentValidation;

namespace Advocacia.Modules.Legal.Application.Features.Alerts;

public sealed class CreateAlertFromPublicationValidator : AbstractValidator<CreateAlertFromPublicationCommand>
{
    public CreateAlertFromPublicationValidator()
    {
        RuleFor(command => command.PublicationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}

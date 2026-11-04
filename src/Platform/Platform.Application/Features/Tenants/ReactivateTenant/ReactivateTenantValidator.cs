using FluentValidation;

namespace Advocacia.Platform.Application.Features.Tenants.ReactivateTenant;

public sealed class ReactivateTenantValidator : AbstractValidator<ReactivateTenantCommand>
{
    public ReactivateTenantValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

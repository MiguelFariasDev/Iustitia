using FluentValidation;

namespace Advocacia.Platform.Application.Features.Tenants.SuspendTenant;

public sealed class SuspendTenantValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

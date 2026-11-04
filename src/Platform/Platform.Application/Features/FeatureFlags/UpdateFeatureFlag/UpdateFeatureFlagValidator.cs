using FluentValidation;

namespace Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;

public sealed class UpdateFeatureFlagValidator : AbstractValidator<UpdateFeatureFlagCommand>
{
    public UpdateFeatureFlagValidator() => RuleFor(x => x.Key).NotEmpty().MaximumLength(200);
}

namespace Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;

public sealed record UpdateFeatureFlagResponse(string Key, bool IsEnabled, DateTimeOffset UpdatedAt);

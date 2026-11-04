namespace Advocacia.Platform.Application.Features.FeatureFlags.GetFeatureFlags;

public sealed record FeatureFlagItem(string Key, bool IsEnabled, DateTimeOffset UpdatedAt);

public sealed record GetFeatureFlagsResponse(IReadOnlyList<FeatureFlagItem> Items);

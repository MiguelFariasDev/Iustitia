namespace Advocacia.Platform.Domain.FeatureFlags;

public readonly record struct FeatureFlagId(Guid Value)
{
    public static FeatureFlagId New() => new(Guid.NewGuid());

    public static FeatureFlagId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

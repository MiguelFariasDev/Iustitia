using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.FeatureFlags;

/// <summary>Chave normalizada de feature flag por tenant (ex.: "athena.enabled").</summary>
public sealed partial class FeatureFlagKey : ValueObject
{
    public string Value { get; }

    private FeatureFlagKey(string value)
    {
        Value = value;
    }

    public static Result<FeatureFlagKey> Create(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<FeatureFlagKey>(ErrorFactory.From(ErrorCode.FEATURE_FLAG_KEY_INVALID));
        }

        var normalized = key.Trim().ToLowerInvariant();

        if (!KeyFormatRegex().IsMatch(normalized))
        {
            return Result.Failure<FeatureFlagKey>(ErrorFactory.From(ErrorCode.FEATURE_FLAG_KEY_INVALID));
        }

        return Result.Success(new FeatureFlagKey(normalized));
    }

    [GeneratedRegex(@"^[a-z0-9_.]+$")]
    private static partial Regex KeyFormatRegex();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

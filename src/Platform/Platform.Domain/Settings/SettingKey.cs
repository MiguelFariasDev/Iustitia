using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Settings;

/// <summary>Chave normalizada de configuração por tenant (ex.: "dashboard.default_layout").</summary>
public sealed partial class SettingKey : ValueObject
{
    public string Value { get; }

    private SettingKey(string value)
    {
        Value = value;
    }

    public static Result<SettingKey> Create(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<SettingKey>(ErrorFactory.From(ErrorCode.SETTINGS_KEY_INVALID));
        }

        var normalized = key.Trim().ToLowerInvariant();

        if (!KeyFormatRegex().IsMatch(normalized))
        {
            return Result.Failure<SettingKey>(ErrorFactory.From(ErrorCode.SETTINGS_KEY_INVALID));
        }

        return Result.Success(new SettingKey(normalized));
    }

    [GeneratedRegex(@"^[a-z0-9_.]+$")]
    private static partial Regex KeyFormatRegex();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Identity;

public sealed partial class Email : ValueObject
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<Email>(ErrorFactory.From(ErrorCode.USER_EMAIL_INVALID));
        }

        var normalized = email.Trim().ToLowerInvariant();

        if (!EmailFormatRegex().IsMatch(normalized))
        {
            return Result.Failure<Email>(ErrorFactory.From(ErrorCode.USER_EMAIL_INVALID));
        }

        return Result.Success(new Email(normalized));
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormatRegex();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Tenancy;

/// <summary>CNPJ validado (14 dígitos, com verificação dos dígitos verificadores).</summary>
public sealed class CNPJ : ValueObject
{
    private static readonly int[] FirstCheckDigitMultipliers = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondCheckDigitMultipliers = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public string Value { get; }

    private CNPJ(string value)
    {
        Value = value;
    }

    public static Result<CNPJ> Create(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return Result.Failure<CNPJ>(ErrorFactory.From(ErrorCode.TENANT_CNPJ_INVALID));
        }

        var digits = new string(cnpj.Where(char.IsDigit).ToArray());

        if (digits.Length != 14)
        {
            return Result.Failure<CNPJ>(ErrorFactory.From(ErrorCode.TENANT_CNPJ_INVALID));
        }

        if (digits.Distinct().Count() == 1)
        {
            return Result.Failure<CNPJ>(ErrorFactory.From(ErrorCode.TENANT_CNPJ_INVALID));
        }

        if (!HasValidCheckDigits(digits))
        {
            return Result.Failure<CNPJ>(ErrorFactory.From(ErrorCode.TENANT_CNPJ_INVALID));
        }

        return Result.Success(new CNPJ(digits));
    }

    private static bool HasValidCheckDigits(string digits)
    {
        var firstCheckDigit = CalculateCheckDigit(digits[..12], FirstCheckDigitMultipliers);
        if (firstCheckDigit != digits[12] - '0')
        {
            return false;
        }

        var secondCheckDigit = CalculateCheckDigit(digits[..13], SecondCheckDigitMultipliers);
        return secondCheckDigit == digits[13] - '0';
    }

    private static int CalculateCheckDigit(string digits, int[] multipliers)
    {
        var sum = digits.Select((c, i) => (c - '0') * multipliers[i]).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

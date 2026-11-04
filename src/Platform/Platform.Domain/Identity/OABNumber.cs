using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Identity;

/// <summary>Número de inscrição na OAB, composto por número + seccional (UF).</summary>
public sealed class OABNumber : ValueObject
{
    // HashSet com comparador case-insensitive (e não uma collection expression): a UF chega
    // do usuário em qualquer caixa ("sp", "SP") e Contains precisa aceitar as duas — uma
    // collection expression não permite passar um IEqualityComparer.
    private static readonly HashSet<string> ValidStateCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
            "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
        };

    public string Number { get; }

    public string StateCode { get; }

    private OABNumber(string number, string stateCode)
    {
        Number = number;
        StateCode = stateCode;
    }

    public static Result<OABNumber> Create(string? number, string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return Result.Failure<OABNumber>(ErrorFactory.From(ErrorCode.USER_OAB_INVALID));
        }

        if (string.IsNullOrWhiteSpace(stateCode) || !ValidStateCodes.Contains(stateCode))
        {
            return Result.Failure<OABNumber>(ErrorFactory.From(ErrorCode.USER_OAB_INVALID));
        }

        var digitsOnly = new string(number.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length == 0)
        {
            return Result.Failure<OABNumber>(ErrorFactory.From(ErrorCode.USER_OAB_INVALID));
        }

        return Result.Success(new OABNumber(digitsOnly, stateCode.ToUpperInvariant()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Number;
        yield return StateCode;
    }

    public override string ToString() => $"{Number}/{StateCode}";
}

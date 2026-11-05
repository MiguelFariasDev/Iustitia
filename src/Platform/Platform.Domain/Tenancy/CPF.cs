using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Domain.Tenancy;

/// <summary>
/// CPF do titular de uma conta Solo, guardado só com dígitos e validado pelos dígitos
/// verificadores. É dado pessoal (LGPD): nunca logar, nunca colocar em evento de domínio.
/// Existe aqui porque Platform não pode depender do módulo Legal, onde mora o Documento
/// dos clientes (ADR-061) — as duas validações implementam a mesma regra.
/// </summary>
public sealed class CPF : ValueObject
{
    public string Value { get; }

    private CPF(string value)
    {
        Value = value;
    }

    public static Result<CPF> Create(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return Result.Failure<CPF>(ErrorFactory.From(ErrorCode.TENANT_CPF_INVALID));
        }

        var digits = new string(cpf.Where(char.IsDigit).ToArray());

        // 11 dígitos iguais passam na conta dos verificadores (000.000.000-00), mas não são CPF.
        if (digits.Length != 11 || digits.Distinct().Count() == 1)
        {
            return Result.Failure<CPF>(ErrorFactory.From(ErrorCode.TENANT_CPF_INVALID));
        }

        return CheckDigit(digits, 9) == digits[9] - '0' && CheckDigit(digits, 10) == digits[10] - '0'
            ? Result.Success(new CPF(digits))
            : Result.Failure<CPF>(ErrorFactory.From(ErrorCode.TENANT_CPF_INVALID));
    }

    private static int CheckDigit(string digits, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (digits[i] - '0') * (length + 1 - i);
        }

        var remainder = sum * 10 % 11;
        return remainder == 10 ? 0 : remainder;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Não expõe o CPF: ToString() vaza fácil em log.</summary>
    public override string ToString() => "CPF(***)";
}

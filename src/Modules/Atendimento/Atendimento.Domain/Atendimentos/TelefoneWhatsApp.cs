using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Atendimentos.Domain.Atendimentos;

/// <summary>
/// Normaliza o número no formato que a WhatsApp Cloud API usa (<c>wa_id</c>): só dígitos,
/// com DDI. Guardar sempre a forma normalizada é o que garante que "+55 (85) 99999-8888" e
/// "5585999998888" caiam no mesmo atendimento.
/// </summary>
public static class TelefoneWhatsApp
{
    public const int MinDigits = 10;
    public const int MaxDigits = 15;

    public static Result<string> Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<string>(ErrorFactory.From(ErrorCode.ATENDIMENTO_PHONE_INVALID));
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());

        return digits.Length is < MinDigits or > MaxDigits
            ? Result.Failure<string>(ErrorFactory.From(ErrorCode.ATENDIMENTO_PHONE_INVALID))
            : Result.Success(digits);
    }
}

using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Shared;

/// <summary>
/// Número único de processo no padrão CNJ (Resolução CNJ nº 65/2008). Vive em Shared, e não
/// sob Publications, porque é o mesmo conceito para os dois agregados do módulo: a Publicação
/// cita o número e o Processo é identificado por ele.
///
/// Formato:
/// <c>NNNNNNN-DD.AAAA.J.TR.OOOO</c> — 20 dígitos ao todo, sendo NNNNNNN o sequencial,
/// DD os dígitos verificadores, AAAA o ano de autuação, J o segmento do Judiciário, TR o
/// tribunal e OOOO a unidade de origem.
///
/// Aceita a entrada com ou sem máscara (o DJEN devolve das duas formas dependendo do
/// campo) e sempre normaliza para a forma mascarada, que é a canônica para exibição e
/// comparação. <see cref="Digits"/> expõe os 20 dígitos puros para índices e joins.
///
/// Os dígitos verificadores são conferidos (mod 97 base 10, ISO 7064): um número com DD
/// errado é um dado corrompido, não um processo desconhecido, e persistir isso
/// contaminaria a vinculação com processos na Fase 2. O job de captura conta e loga essas
/// rejeições em vez de abortar a página (ver CnjCaptureJob).
/// </summary>
public sealed partial class CNJNumber : ValueObject
{
    /// <summary>Forma canônica mascarada: NNNNNNN-DD.AAAA.J.TR.OOOO.</summary>
    public string Value { get; }

    /// <summary>Os mesmos 20 dígitos, sem separadores.</summary>
    public string Digits { get; }

    public int Sequential => int.Parse(Digits[..7], CultureInfo.InvariantCulture);

    public int Year => int.Parse(Digits.Substring(9, 4), CultureInfo.InvariantCulture);

    public int Segment => int.Parse(Digits.Substring(13, 1), CultureInfo.InvariantCulture);

    public string Court => Digits.Substring(14, 2);

    public string OriginUnit => Digits.Substring(16, 4);

    /// <summary>
    /// Sigla do tribunal derivada de <see cref="Segment"/> + <see cref="Court"/> — ex.:
    /// "8.26" devolve "TJSP". Nulo quando a combinação não corresponde a tribunal algum,
    /// o que é possível mesmo num número com dígitos verificadores corretos.
    /// </summary>
    public string? Tribunal => TribunalResolver.Resolve(Segment, Court);

    private CNJNumber(string value, string digits)
    {
        Value = value;
        Digits = digits;
    }

    public static Result<CNJNumber> Create(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return Result.Failure<CNJNumber>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_CNJ));
        }

        var digits = NonDigitsRegex().Replace(number, string.Empty);

        if (digits.Length != 20)
        {
            return Result.Failure<CNJNumber>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_CNJ));
        }

        // Segmento 0 não existe (1..9, conforme o art. 1º, §1º, V da Resolução 65/2008).
        if (digits[13] == '0')
        {
            return Result.Failure<CNJNumber>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_CNJ));
        }

        if (!HasValidCheckDigits(digits))
        {
            return Result.Failure<CNJNumber>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_CNJ));
        }

        return Result.Success(new CNJNumber(Format(digits), digits));
    }

    /// <summary>
    /// Verificação mod 97 base 10 (ISO 7064) definida pela Resolução CNJ nº 65/2008: com os
    /// dígitos verificadores removidos e "00" concatenado ao final, o resto da divisão do
    /// número por 97 deve ser 98 − DD. BigInteger porque o número reordenado tem 20 dígitos
    /// e não cabe em long.
    /// </summary>
    private static bool HasValidCheckDigits(string digits)
    {
        var checkDigits = int.Parse(digits.Substring(7, 2), CultureInfo.InvariantCulture);
        var withoutCheckDigits = digits[..7] + digits[9..] + "00";

        var remainder = (int)(BigInteger.Parse(withoutCheckDigits, CultureInfo.InvariantCulture) % 97);

        return 98 - remainder == checkDigits;
    }

    private static string Format(string digits) =>
        $"{digits[..7]}-{digits.Substring(7, 2)}.{digits.Substring(9, 4)}.{digits.Substring(13, 1)}.{digits.Substring(14, 2)}.{digits.Substring(16, 4)}";

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitsRegex();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Digits;
    }

    public override string ToString() => Value;
}

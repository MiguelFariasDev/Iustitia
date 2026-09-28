using System.Text.RegularExpressions;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Mascara dados sensíveis (CPF, CNPJ, e-mail, telefone, tokens) antes de irem para
/// qualquer sink de log — ver ADR-037 e docs/compliance/lgpd.md. Função pura, sem
/// dependência do Serilog, para ser testável isoladamente (ver
/// <see cref="SensitiveDataMaskingEnricher"/> para o ponto de integração real).
/// Senhas nunca passam por aqui: a regra é simplesmente nunca logar o campo de senha,
/// não existe um padrão de texto genérico e seguro para "parece uma senha".
/// </summary>
public static partial class LogMaskingPolicy
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var masked = CnpjRegex().Replace(value, "**.***.***/****-**");
        masked = CpfRegex().Replace(masked, "***.***.***-**");
        masked = BearerTokenRegex().Replace(masked, "Bearer ***");
        masked = PhoneRegex().Replace(masked, match => $"({match.Groups["ddd"].Value}) *****-****");
        masked = EmailRegex().Replace(masked, MaskEmailMatch);

        return masked;
    }

    private static string MaskEmailMatch(Match match)
    {
        var localPart = match.Groups["local"].Value;
        var domain = match.Groups["domain"].Value;
        var visibleLength = Math.Min(2, localPart.Length);

        return $"{localPart[..visibleLength]}***@{domain}";
    }

    // CNPJ precisa ser verificado ANTES de CPF: o formato de CNPJ (00.000.000/0000-00)
    // contém um trecho "00.000.000" que também bateria com o início do padrão de CPF.
    [GeneratedRegex(@"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b")]
    private static partial Regex CnpjRegex();

    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b")]
    private static partial Regex CpfRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9\-_.]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"\((?<ddd>\d{2})\)\s?\d{4,5}-\d{4}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?<local>[A-Za-z0-9._%+-]+)@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})\b")]
    private static partial Regex EmailRegex();
}

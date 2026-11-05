namespace Advocacia.Modules.Legal.Infrastructure.Consulta;

/// <summary>
/// Casa o telefone que o WhatsApp informa (<c>wa_id</c>: DDI + DDD + número, só dígitos) com o formato
/// cadastrado em Legal (DDD + número, 10 ou 11 dígitos, sem DDI).
///
/// Dois cuidados: só números brasileiros (DDI 55) casam — o cadastro não guarda DDI, e tratar
/// "+1 415…" como brasileiro cruzaria pessoas diferentes —; e o nono dígito, porque o WhatsApp ainda
/// entrega alguns celulares antigos sem ele (5585 9999-8888) enquanto o cadastro costuma ter com
/// (85 99999-8888), ou o contrário. As duas formas são candidatas.
/// </summary>
public static class BrazilianPhone
{
    public static IReadOnlyList<string> NationalCandidates(string? whatsAppPhone)
    {
        var digits = new string((whatsAppPhone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        if (!digits.StartsWith("55", StringComparison.Ordinal) || digits.Length is not (12 or 13))
        {
            return [];
        }

        var national = digits[2..];
        var candidates = new List<string> { national };

        if (national.Length == 10)
        {
            candidates.Add(string.Concat(national.AsSpan(0, 2), "9", national.AsSpan(2)));
        }
        else if (national[2] == '9')
        {
            candidates.Add(string.Concat(national.AsSpan(0, 2), national.AsSpan(3)));
        }

        return candidates;
    }
}

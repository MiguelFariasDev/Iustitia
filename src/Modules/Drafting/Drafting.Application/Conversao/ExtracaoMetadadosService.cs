using System.Globalization;
using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Validacao;

namespace Advocacia.Modules.Drafting.Application.Conversao;

/// <summary>Extração por regras: datas, valores, processos e partes que ESTÃO no texto. Sem IA, sem inferência.</summary>
public sealed partial class ExtracaoMetadadosService : IExtracaoMetadadosService
{
    private const int MaxItens = 25;

    public MetadadosExtraidos Extrair(string texto)
    {
        var datas = ValidadorCitacoes.DatasDe(texto).Order().ToList();
        var valores = Valor().Matches(texto).Select(m => Regex.Replace(m.Value, @"\s+", " ").Trim())
            .Distinct(StringComparer.Ordinal).Take(MaxItens).ToList();
        var processos = Processo().Matches(texto).Select(m => m.Value).Distinct(StringComparer.Ordinal).Take(MaxItens).ToList();
        var partes = Parte().Matches(texto)
            .Select(m => m.Groups["nome"].Value.Trim(' ', ',', '.', ';', '*', '"', '“', '”'))
            .Where(n => n.Length is >= 3 and <= 120)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxItens)
            .ToList();

        return new MetadadosExtraidos(
            DataDoDocumento(texto, datas), datas.Take(MaxItens).Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList(),
            valores, processos, partes);
    }

    /// <summary>"Fortaleza, 10 de março de 2024" (local e data) é a data do documento; senão, só se houver uma única data.</summary>
    private static DateOnly? DataDoDocumento(string texto, List<DateOnly> datas)
    {
        var assinaturas = LocalEData().Matches(texto);
        if (assinaturas.Count > 0)
        {
            var encontradas = ValidadorCitacoes.DatasDe(assinaturas[^1].Value);
            if (encontradas.Count == 1)
            {
                return encontradas.Single();
            }
        }

        return datas.Count == 1 ? datas[0] : null;
    }

    [GeneratedRegex(@"R\$\s*\d[\d\.]*(?:,\d{1,2})?", RegexOptions.CultureInvariant)]
    private static partial Regex Valor();

    [GeneratedRegex(@"\b\d{7}-\d{2}\.\d{4}\.\d\.\d{2}\.\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Processo();

    [GeneratedRegex(
        @"(?:CONTRATANTE|CONTRATAD[AO]|LOCADOR[A]?|LOCAT[ÁA]RI[AO]|AUTOR[A]?|R[ÉE]U|R[ÉE]|REQUERENTE|REQUERID[AO]|RECLAMANTE|RECLAMAD[AO]|EXEQUENTE|EXECUTAD[AO]|APELANTE|APELAD[AO]|AGRAVANTE|AGRAVAD[AO]|VENDEDOR[A]?|COMPRADOR[A]?|CREDOR[A]?|DEVEDOR[A]?|OUTORGANTE|OUTORGAD[AO])\**\s*[:\-–—]\s*\**(?<nome>[^\n,;]{3,120})",
        RegexOptions.CultureInvariant)]
    private static partial Regex Parte();

    [GeneratedRegex(
        @"[A-ZÀ-Ú][\p{L} ]{2,40}(?:\s*[-/(]\s*[A-Z]{2}\)?)?,\s*(?:aos\s+)?\d{1,2}[ºo°]?\s+de\s+\p{L}+\s+de\s+\d{4}",
        RegexOptions.CultureInvariant)]
    private static partial Regex LocalEData();
}

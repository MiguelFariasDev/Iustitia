using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Busca;

public enum TipoReferencia
{
    /// <summary>Artigo (ou a norma inteira) de lei, decreto ou da Constituição.</summary>
    Norma = 0,
    Sumula = 1,
    Tema = 2,
}

/// <summary>Um dispositivo citado pelo nome: vira busca EXATA, que nem vetor nem BM25 garantem.</summary>
public sealed record ReferenciaLegal(
    TipoReferencia Tipo, string? Numero, string? Artigo, string? Tribunal, bool Constituicao = false, bool Vinculante = false);

/// <summary>Extrai de um texto livre as referências a artigos, leis, súmulas e temas.</summary>
public static partial class ExtratorReferencias
{
    private const string Codigos =
        @"(?<nome>Constitui[çc][ãa]o(?:\s+Federal)?|CRFB(?:/88)?|CF(?:/88)?|C[óo]digo\s+(?:de\s+Processo\s+)?(?:Civil|Penal)|C[óo]digo\s+de\s+Defesa\s+do\s+Consumidor|C[óo]digo\s+Tribut[áa]rio\s+Nacional|Consolida[çc][ãa]o\s+das\s+Leis\s+do\s+Trabalho|N?CPC|CDC|CLT|CTN|CPP|CP|CC|ECA|LGPD|LINDB)";

    [GeneratedRegex(@"\barts?(?:igos?)?\.?\s*(?<art>\d{1,3}(?:\.\d{3})*(?:[ºo°])?(?:-[A-Z])?(?:\s*(?:,|e)\s*\d{1,3}(?:\.\d{3})*(?:[ºo°])?(?:-[A-Z])?)*)(?:[^.;\n]{0,40}?)\b(?:d[oae]s?|n[oa]s?|,)\s*(?:a\s+)?" + Codigos + @"\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ArtigoDeCodigo();

    [GeneratedRegex(@"\b" + Codigos + @"\b\s*,?\s*arts?(?:igos?)?\.?\s*(?<art>\d{1,3}(?:\.\d{3})*(?:[ºo°])?(?:-[A-Z])?(?:\s*(?:,|e)\s*\d{1,3}(?:\.\d{3})*(?:[ºo°])?(?:-[A-Z])?)*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CodigoArtigo();

    [GeneratedRegex(@"\b(?:lei(?:\s+complementar|\s+federal|\s+ordin[áa]ria)?|decreto(?:-lei)?|medida\s+provis[óo]ria)\s*(?:n[ºo°.]*\s*)?(?<num>\d{1,3}(?:\.\d{3})+|\d{3,6})(?!\d)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex NumeroDeLei();

    [GeneratedRegex(@"\barts?(?:igos?)?\.?\s*(?<art>\d{1,3}(?:\.\d{3})*(?:[ºo°])?(?:-[A-Z])?)[^.;\n]{0,40}?\b(?:lei(?:\s+complementar|\s+federal)?|decreto(?:-lei)?)\s*(?:n[ºo°.]*\s*)?(?<num>\d{1,3}(?:\.\d{3})+|\d{3,6})(?!\d)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ArtigoDeLei();

    [GeneratedRegex(@"\bs[úu]mulas?\s+(?<v>vinculante\s+)?(?:n[ºo°.]*\s*)?(?<n>\d{1,6})(?!\d)(?:\s*,?\s*(?:d[oa]\s+)?(?<t>STF|STJ|TST|TSE|STM|TNU))?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Sumula();

    [GeneratedRegex(@"\btema\s+(?:repetitivo\s+|de\s+repercuss[ãa]o\s+geral\s+)?(?:n[ºo°.]*\s*)?(?<n>\d{1,3}(?:\.\d{3})|\d{1,6})(?!\d)(?:\s*,?\s*(?:d[oa]\s+)?(?<t>STF|STJ|TST))?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Tema();

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})*(?:[ºo°])?(?:-[A-Z])?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UmArtigo();

    public static IReadOnlyList<ReferenciaLegal> Extrair(string texto, int max = 20)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var refs = new List<ReferenciaLegal>();

        foreach (var match in ArtigoDeCodigo().Matches(texto).Concat(CodigoArtigo().Matches(texto)))
        {
            var norma = NormasConhecidas.PorNome(match.Groups["nome"].Value);
            if (norma is null)
            {
                continue;
            }

            foreach (Match artigo in UmArtigo().Matches(match.Groups["art"].Value))
            {
                refs.Add(new ReferenciaLegal(
                    TipoReferencia.Norma, norma.Numero, NormalizarArtigo(artigo.Value), null, Constituicao: norma.Numero is null));
            }
        }

        foreach (Match match in ArtigoDeLei().Matches(texto))
        {
            refs.Add(new ReferenciaLegal(
                TipoReferencia.Norma, SoDigitos(match.Groups["num"].Value), NormalizarArtigo(match.Groups["art"].Value), null));
        }

        foreach (Match match in NumeroDeLei().Matches(texto))
        {
            var numero = SoDigitos(match.Groups["num"].Value);
            if (!refs.Any(r => r.Numero == numero))
            {
                refs.Add(new ReferenciaLegal(TipoReferencia.Norma, numero, null, null));
            }
        }

        foreach (Match match in Sumula().Matches(texto))
        {
            refs.Add(new ReferenciaLegal(
                TipoReferencia.Sumula, match.Groups["n"].Value, null,
                match.Groups["t"].Success ? match.Groups["t"].Value.ToUpperInvariant() : null,
                Vinculante: match.Groups["v"].Success));
        }

        foreach (Match match in Tema().Matches(texto))
        {
            refs.Add(new ReferenciaLegal(
                TipoReferencia.Tema, SoDigitos(match.Groups["n"].Value), null,
                match.Groups["t"].Success ? match.Groups["t"].Value.ToUpperInvariant() : null));
        }

        return refs.Distinct().Take(max).ToList();
    }

    /// <summary>"5º", "5o" e "5" são o mesmo artigo; "186-A" não é "186".</summary>
    public static string NormalizarArtigo(string artigo)
    {
        var limpo = artigo.Trim().ToUpperInvariant().Replace(".", string.Empty);
        var sufixo = string.Empty;
        var hifen = limpo.IndexOf('-');
        if (hifen > 0)
        {
            sufixo = limpo[hifen..].TrimEnd(' ', ',');
            limpo = limpo[..hifen];
        }

        var digitos = new string(limpo.TakeWhile(char.IsDigit).ToArray());

        return digitos.Length == 0 ? artigo.Trim() : digitos + sufixo;
    }

    private static string SoDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}

using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>Extração de termos para a busca por palavra-chave e para o reranking (puro, sem IO).</summary>
public static partial class NormaTexto
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "para", "como", "com", "sem", "sobre", "entre", "pela", "pelo", "pelas", "pelos", "essa", "esse", "esta", "este",
        "isso", "isto", "aquele", "aquela", "quando", "onde", "que", "qual", "quais", "foram", "sendo", "será", "serão",
        "seus", "suas", "dele", "dela", "deste", "desta", "nesse", "nessa", "mais", "menos", "muito", "também", "ainda",
        "autor", "autora", "réu", "ré", "requerente", "requerido", "requerida", "pede", "pedido", "pedidos", "fatos",
        "petição", "inicial", "contestação", "recurso", "apelação", "dias", "data", "valor", "conforme", "devido", "devida",
    };

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Palavra();

    [GeneratedRegex(@"\bart(?:igo)?s?\.?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArtigoMencionado();

    [GeneratedRegex(@"\b(?:lei|decreto|resolu[çc][ãa]o|s[úu]mula|portaria|instru[çc][ãa]o normativa)(?:\s+complementar|\s+federal|\s+vinculante)?\s*(?:n[ºo°.]*\s*)?(\d[\d\.]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NormaMencionada();

    /// <summary>Termos distintos, minúsculos, ≥ 4 letras, sem stopwords, os mais longos primeiro (mais específicos).</summary>
    public static IReadOnlyList<string> Termos(string texto, int max = 12) =>
        Palavra().Matches(texto.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length >= 4 && !Stopwords.Contains(t) && !t.All(char.IsDigit))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(t => t.Length)
            .ThenBy(t => t, StringComparer.Ordinal)
            .Take(max)
            .ToList();

    public static IReadOnlySet<string> ArtigosMencionados(string texto) =>
        ArtigoMencionado().Matches(texto).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>Números de norma citados na consulta, sem pontos ("10.406" → "10406").</summary>
    public static IReadOnlySet<string> NormasMencionadas(string texto) =>
        NormaMencionada().Matches(texto).Select(m => m.Groups[1].Value.Replace(".", string.Empty).TrimEnd()).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal);

    /// <summary>"186" de "186-A" ou "186º": só os dígitos iniciais, para comparar artigo citado × artigo do trecho.</summary>
    public static string? NumeroDoArtigo(string? artigo)
    {
        if (string.IsNullOrWhiteSpace(artigo))
        {
            return null;
        }

        var digits = new string(artigo.Replace(".", string.Empty).TakeWhile(char.IsDigit).ToArray());

        return digits.Length == 0 ? null : digits;
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Busca;

/// <summary>Vetor esparso (índice do termo → peso), no formato do Qdrant.</summary>
public sealed record VetorEsparso(uint[] Indices, float[] Valores)
{
    public bool Vazio => Indices.Length == 0;
}

/// <summary>
/// BM25 de verdade, sem serviço externo: aqui se calcula a parte do DOCUMENTO (frequência saturada por k1 e
/// normalizada pelo tamanho, b) e da CONSULTA (peso 1 por termo); o IDF é calculado pelo próprio Qdrant, na
/// coleção inteira, a cada busca (<c>Modifier.Idf</c>). O produto dos dois é a fórmula do BM25 — e o IDF
/// nunca fica velho quando o acervo cresce.
/// </summary>
public static partial class Bm25
{
    private const double K1 = 1.2;
    private const double B = 0.75;

    /// <summary>Tamanho médio esperado de um trecho, em termos indexáveis (trechos de 400–900 tokens).</summary>
    private const double TamanhoMedio = 250;

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "o", "as", "os", "um", "uma", "uns", "umas", "de", "do", "da", "dos", "das", "em", "no", "na", "nos", "nas",
        "por", "pelo", "pela", "pelos", "pelas", "para", "com", "sem", "sob", "sobre", "e", "ou", "que", "se", "ao", "aos",
        "como", "mais", "mas", "foi", "ser", "sua", "seu", "suas", "seus", "este", "esta", "esse", "essa", "isso", "isto",
        "quando", "qual", "quais", "cujo", "cuja", "entre", "ate", "ja", "nao", "sao", "tem", "sera", "seja", "lhe", "nem",
    };

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})+|\d+|[\p{L}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Termo();

    public static VetorEsparso Documento(string texto)
    {
        var termos = Tokenizar(texto);
        if (termos.Count == 0)
        {
            return new VetorEsparso([], []);
        }

        var norma = K1 * (1 - B + (B * termos.Count / TamanhoMedio));
        var pesos = new SortedDictionary<uint, float>();
        foreach (var grupo in termos.GroupBy(t => t, StringComparer.Ordinal))
        {
            var tf = grupo.Count();
            var indice = Indice(grupo.Key);

            // Colisão de hash (raríssima em 32 bits): fica o maior peso, nunca a soma de termos diferentes.
            var peso = (float)(tf * (K1 + 1) / (tf + norma));
            pesos[indice] = pesos.TryGetValue(indice, out var atual) ? Math.Max(atual, peso) : peso;
        }

        return new VetorEsparso([.. pesos.Keys], [.. pesos.Values]);
    }

    public static VetorEsparso Consulta(string texto)
    {
        var indices = Tokenizar(texto).Distinct(StringComparer.Ordinal).Select(Indice).Distinct().Order().ToArray();

        return new VetorEsparso(indices, [.. Enumerable.Repeat(1f, indices.Length)]);
    }

    /// <summary>Minúsculas, sem acento, sem stopwords, números sem ponto de milhar e radical leve do português.</summary>
    public static IReadOnlyList<string> Tokenizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var termos = new List<string>();
        foreach (Match match in Termo().Matches(SemAcento(texto.ToLowerInvariant())))
        {
            var termo = match.Value;
            if (char.IsDigit(termo[0]))
            {
                termos.Add(termo.Replace(".", string.Empty));
                continue;
            }

            if (termo.Length < 2 || Stopwords.Contains(termo))
            {
                continue;
            }

            termos.Add(Radical(termo));
        }

        return termos;
    }

    /// <summary>
    /// Radical leve: plural e vogal temática. Conservador de propósito — juntar "contrato" e "contratos"
    /// ajuda; juntar "prescrição" e "prescrever" atrapalha a busca exata que o BM25 existe para fazer.
    /// </summary>
    internal static string Radical(string termo)
    {
        if (termo.Length <= 3)
        {
            return termo;
        }

        var t = termo switch
        {
            _ when termo.EndsWith("oes", StringComparison.Ordinal) || termo.EndsWith("aes", StringComparison.Ordinal) => termo[..^3] + "ao",
            _ when termo.EndsWith("ais", StringComparison.Ordinal) && termo.Length > 4 => termo[..^2] + "l",
            _ when termo.EndsWith("eis", StringComparison.Ordinal) && termo.Length > 4 => termo[..^3] + "el",
            _ when termo.EndsWith("ns", StringComparison.Ordinal) => termo[..^2] + "m",
            _ when termo.EndsWith("res", StringComparison.Ordinal) || termo.EndsWith("zes", StringComparison.Ordinal) => termo[..^2],
            _ when termo.EndsWith('s') && !termo.EndsWith("ss", StringComparison.Ordinal) => termo[..^1],
            _ => termo,
        };

        return t.Length > 4 && t[^1] is 'a' or 'o' or 'e' && !t.EndsWith("ao", StringComparison.Ordinal) && t.Length > 4 ? t[..^1] : t;
    }

    /// <summary>FNV-1a de 32 bits: estável entre processos e versões (o <c>GetHashCode</c> não é).</summary>
    internal static uint Indice(string termo)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(termo))
        {
            hash ^= b;
            hash *= 16777619u;
        }

        return hash;
    }

    internal static string SemAcento(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Services;

public sealed record CitationReport(
    IReadOnlySet<string> CitedLabels,
    IReadOnlyList<string> UnknownCitations,
    IReadOnlyList<string> UnsupportedReferences);

/// <summary>
/// Confere, DEPOIS da geração, o que o modelo prometeu: as citações [ARQUIVO, p. X] existem entre
/// os trechos recuperados? Há artigo ou súmula no texto que não aparece em nenhuma fonte nem nos
/// dados do caso? É uma checagem determinística e barata — não prova que a minuta está certa,
/// só aponta o que o advogado precisa conferir primeiro (regras 2, 3 e 4 do system prompt).
/// </summary>
public static partial class CitationVerifier
{
    [GeneratedRegex(@"\[([^\[\]\n]{1,300}?,\s*p\.\s*[^\[\]\n]{1,20}?)\]", RegexOptions.CultureInvariant)]
    private static partial Regex CitationPattern();

    [GeneratedRegex(@"\b(art(?:igo)?s?\.?\s*\d+[ºo°]?(?:-[A-Z])?|s[úu]mula\s+(?:vinculante\s+)?(?:n[ºo°.]?\s*)?\d+|resp\s*\d[\d\.\-/]*|adi\s*\d+|adpf\s*\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    public static CitationReport Verify(string generatedText, IReadOnlyList<RetrievedChunk> chunks, string caseText)
    {
        var allowed = chunks
            .Select(chunk => Normalize(chunk.Rotulo))
            .ToHashSet(StringComparer.Ordinal);

        var cited = new HashSet<string>(StringComparer.Ordinal);
        var unknown = new List<string>();

        foreach (Match match in CitationPattern().Matches(generatedText))
        {
            var label = Normalize(match.Groups[1].Value);
            if (allowed.Contains(label))
            {
                cited.Add(label);
            }
            else if (!unknown.Contains(match.Groups[1].Value.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                unknown.Add(match.Groups[1].Value.Trim());
            }
        }

        var support = Normalize(caseText + "\n" + string.Join('\n', chunks.Select(chunk => chunk.Texto)));
        var unsupported = ReferencePattern().Matches(generatedText)
            .Select(match => match.Value.Trim())
            .Where(reference => !support.Contains(Normalize(reference), StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CitationReport(cited, unknown, unsupported);
    }

    public static bool WasCited(RetrievedChunk chunk, CitationReport report) =>
        report.CitedLabels.Contains(Normalize(chunk.Rotulo));

    /// <summary>Caixa, espaços e pontuação de "art. 186" × "Art 186" não podem gerar falso alarme.</summary>
    private static string Normalize(string text)
    {
        var chars = text.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ',' || c == ' ')
            .ToArray();

        return Regex.Replace(new string(chars), @"\s+", " ").Trim();
    }
}

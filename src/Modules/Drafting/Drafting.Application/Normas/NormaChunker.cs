using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Normas;

namespace Advocacia.Modules.Drafting.Application.Normas;

public sealed record ChunkDraft(
    int Ordem, string Texto, string? Secao, string? Artigo, string? Paragrafo, string? Inciso, int Tokens, string Hash);

/// <summary>
/// Chunker hierárquico e consciente da estrutura jurídica — NÃO é janela de caracteres.
/// Unidade natural = ARTIGO (nunca dividido se couber em <see cref="ChunkingOptions.MaxTokens"/>).
/// Artigo longo: divide em § (parágrafo) → inciso → alínea → parágrafo de texto → frase, sempre em
/// fronteira de dispositivo. Todo chunk carrega título da norma, ementa curta, hierarquia
/// (parte/livro/título/capítulo/seção) e referência do dispositivo; trechos de continuação repetem o
/// caput como contexto. Overlap só no último recurso (corte por frase), que é o único em que um
/// pensamento pode ser partido ao meio.
/// Documentos sem artigos (súmulas, decisões, tratados em prosa) caem para seções/parágrafos.
/// </summary>
public sealed partial class NormaChunker(ChunkingOptions options)
{
    private static readonly Regex Artigo = ArtigoRegex();
    private static readonly Regex Paragrafo = ParagrafoRegex();
    private static readonly Regex Inciso = IncisoRegex();
    private static readonly Regex Alinea = AlineaRegex();
    private static readonly Regex Heading = HeadingRegex();
    private static readonly Regex SoRevogado = SoRevogadoRegex();
    private static readonly Regex Frase = FraseRegex();

    private static readonly (string Chave, int Rank)[] Ranks =
    [
        ("PARTE", 1), ("LIVRO", 2), ("TÍTULO", 3), ("TITULO", 3), ("CAPÍTULO", 4), ("CAPITULO", 4),
        ("SEÇÃO", 5), ("SECAO", 5), ("SUBSEÇÃO", 6), ("SUBSECAO", 6),
        // ADCT e anexos reiniciam a numeração de artigos: precisam aparecer na hierarquia do trecho.
        ("ATO DAS", 1), ("ANEXO", 1),
    ];

    public IReadOnlyList<ChunkDraft> Dividir(NormaMetadata meta, string markdownBody)
    {
        var body = JsonBlockFlattener.Flatten(markdownBody).Replace("\r\n", "\n").Replace("**", string.Empty);
        var units = ParseUnits(meta.Titulo, body);
        var result = new List<ChunkDraft>();

        foreach (var unit in units)
        {
            var lines = unit.Linhas.ToList();
            TrimBlank(lines);
            if (lines.Count == 0 || IsRevogadoSomente(unit, lines))
            {
                continue;
            }

            foreach (var part in Split(lines, unit.Artigo is not null ? ArtigoLevels : TextoLevels, 0, null))
            {
                var text = Render(meta, unit, part, unit.Artigo is not null ? lines[0] : null);
                var tokens = TokenEstimator.Estimate(text, options.CharsPorToken);
                result.Add(new ChunkDraft(
                    result.Count, text, unit.Hierarquia, unit.Artigo, part.Paragrafo, part.Inciso, tokens, Hash(text)));
            }
        }

        return result;
    }

    // ------------------------------------------------------------ leitura em unidades

    private sealed class Segmento(string? artigo, string? hierarquia)
    {
        public string? Artigo { get; } = artigo;

        public string? Hierarquia { get; } = hierarquia;

        public List<string> Linhas { get; } = [];
    }

    private List<Segmento> ParseUnits(string titulo, string body)
    {
        var units = new List<Segmento>();
        var hierarquia = new SortedDictionary<int, string>();
        Segmento current = new(null, null);

        string? PathNow() => hierarquia.Count == 0 ? null : string.Join(" > ", hierarquia.Values);

        void Close()
        {
            if (current.Linhas.Any(l => l.Trim().Length > 0))
            {
                units.Add(current);
            }
        }

        foreach (var raw in body.Split('\n'))
        {
            var line = raw.TrimEnd();
            var heading = Heading.Match(line);
            if (heading.Success)
            {
                var textHeading = heading.Groups[2].Value.Trim();
                var level = heading.Groups[1].Length;
                if (level == 1 && SameTitle(textHeading, titulo))
                {
                    continue;
                }

                var rank = RankOf(textHeading);
                if (rank > 0)
                {
                    Close();
                    foreach (var key in hierarquia.Keys.Where(k => k >= rank).ToList())
                    {
                        hierarquia.Remove(key);
                    }

                    hierarquia[rank] = Trunc(textHeading, 120);
                    current = new Segmento(null, PathNow());
                    continue;
                }

                // Título sem palavra-chave estrutural (ex.: "## Registro 263", "### tese"): vira conteúdo.
                current.Linhas.Add(line);
                continue;
            }

            var article = Artigo.Match(line);
            if (article.Success)
            {
                Close();
                current = new Segmento(NormalizeArtigo(article.Groups["n"].Value), PathNow());
            }

            current.Linhas.Add(line);
        }

        Close();

        return units;
    }

    private static int RankOf(string heading)
    {
        var upper = heading.ToUpperInvariant();
        foreach (var (key, rank) in Ranks)
        {
            if (upper.StartsWith(key, StringComparison.Ordinal))
            {
                return rank;
            }
        }

        return 0;
    }

    private static bool SameTitle(string heading, string titulo) =>
        string.Equals(heading.Trim(), titulo.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeArtigo(string raw) => raw.Trim().TrimEnd('.', ',', ' ');

    private static bool IsRevogadoSomente(Segmento unit, List<string> lines)
    {
        if (unit.Artigo is null)
        {
            return false;
        }

        var text = string.Join(' ', lines);

        return text.Length < 260 && SoRevogado.IsMatch(text);
    }

    private static void TrimBlank(List<string> lines)
    {
        while (lines.Count > 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    // ------------------------------------------------------------ divisão

    private sealed record Part(List<string> Linhas, string? Paragrafo, string? Inciso, bool PrecisaCaput);

    private static readonly Regex Blank = new(@"^\s*$", RegexOptions.Compiled);
    private static readonly Regex[] ArtigoLevels = [Paragrafo, Inciso, Alinea];
    private static readonly Regex[] TextoLevels = [Heading, Blank];

    private int Tok(IEnumerable<string> lines) =>
        TokenEstimator.Estimate(string.Join('\n', lines), options.CharsPorToken);

    private IEnumerable<Part> Split(List<string> lines, Regex[] levels, int level, string? paragrafoCtx)
    {
        if (Tok(lines) <= options.MaxTokens)
        {
            yield return new Part(lines, paragrafoCtx, null, paragrafoCtx is not null);
            yield break;
        }

        if (level >= levels.Length)
        {
            foreach (var chunk in SplitSentences(lines))
            {
                yield return new Part(chunk, paragrafoCtx, null, true);
            }

            yield break;
        }

        var blocks = Partition(lines, levels[level]);
        if (blocks.Count <= 1)
        {
            foreach (var part in Split(lines, levels, level + 1, paragrafoCtx))
            {
                yield return part;
            }

            yield break;
        }

        var group = new List<string>();
        string? groupParagrafo = paragrafoCtx;
        string? groupInciso = null;
        var first = true;

        IEnumerable<Part> Emit()
        {
            if (group.Count > 0)
            {
                yield return new Part([.. group], groupParagrafo, groupInciso, !first);
            }
        }

        foreach (var block in blocks)
        {
            var label = LabelOf(block[0], levels[level]);
            var blockTokens = Tok(block);

            if (blockTokens > options.MaxTokens)
            {
                foreach (var part in Emit())
                {
                    yield return part;
                }

                group.Clear();
                first = false;
                var ctx = levels[level] == Paragrafo ? label ?? paragrafoCtx : paragrafoCtx;
                foreach (var part in Split(block, levels, level + 1, ctx))
                {
                    yield return part;
                }

                continue;
            }

            if (group.Count > 0 && Tok(group) + blockTokens > options.AlvoTokens)
            {
                foreach (var part in Emit())
                {
                    yield return part;
                }

                group.Clear();
                first = false;
            }

            if (group.Count == 0)
            {
                groupParagrafo = levels[level] == Paragrafo ? label ?? paragrafoCtx : paragrafoCtx;
                groupInciso = levels[level] == Inciso ? label : null;
            }

            group.AddRange(block);
        }

        foreach (var part in Emit())
        {
            yield return part;
        }
    }

    /// <summary>Divide em blocos que COMEÇAM numa linha do padrão (o que vem antes é o primeiro bloco).</summary>
    private static List<List<string>> Partition(List<string> lines, Regex marker)
    {
        var blocks = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in lines)
        {
            var isBoundary = marker.IsMatch(line) && (marker != Blank || current.Count > 0);
            if (marker == Blank)
            {
                // Linha em branco ENCERRA o bloco (e não abre outro).
                if (line.Trim().Length == 0)
                {
                    if (current.Count > 0)
                    {
                        blocks.Add(current);
                        current = [];
                    }

                    continue;
                }
            }
            else if (isBoundary && current.Count > 0)
            {
                blocks.Add(current);
                current = [];
            }

            current.Add(line);
        }

        if (current.Count > 0)
        {
            blocks.Add(current);
        }

        return blocks;
    }

    /// <summary>Último recurso: frases, com UMA frase de overlap para o pensamento não ser cortado seco.</summary>
    private IEnumerable<List<string>> SplitSentences(List<string> lines)
    {
        var sentences = Frase.Split(string.Join(' ', lines.Where(l => l.Trim().Length > 0))).Where(s => s.Length > 0).ToList();
        var group = new List<string>();
        string? previous = null;
        foreach (var sentence in sentences)
        {
            var parts = Hard(sentence);
            foreach (var piece in parts)
            {
                if (group.Count > 0 && Tok([string.Join(' ', group), piece]) > options.AlvoTokens)
                {
                    yield return [string.Join(' ', group)];
                    group = previous is null ? [] : [previous];
                }

                group.Add(piece);
                previous = piece;
            }
        }

        if (group.Count > 0)
        {
            yield return [string.Join(' ', group)];
        }
    }

    /// <summary>Frase maior que o teto (tabela, lista sem pontuação): corta em espaço.</summary>
    private IEnumerable<string> Hard(string sentence)
    {
        var maxChars = (int)(options.MaxTokens * options.CharsPorToken);
        while (sentence.Length > maxChars)
        {
            var cut = sentence.LastIndexOf(' ', maxChars);
            if (cut <= 0)
            {
                cut = maxChars;
            }

            yield return sentence[..cut];
            sentence = sentence[cut..].TrimStart();
        }

        yield return sentence;
    }

    /// <summary>Rótulo do dispositivo que ABRE o bloco, conforme o nível em que se divide (§ ou inciso).</summary>
    private static string? LabelOf(string firstLine, Regex marker)
    {
        if (marker == Paragrafo && Paragrafo.Match(firstLine) is { Success: true } p)
        {
            return p.Groups["l"].Value.Trim().TrimEnd('.', '-', '–', '—', ' ');
        }

        return marker == Inciso && Inciso.Match(firstLine) is { Success: true } i ? i.Groups["l"].Value : null;
    }

    // ------------------------------------------------------------ montagem do texto

    private string Render(NormaMetadata meta, Segmento unit, Part part, string? caputLine)
    {
        var sb = new StringBuilder();
        var dispositivo = unit.Artigo is null ? null : $"Art. {unit.Artigo}";
        if (dispositivo is not null && part.Paragrafo is not null)
        {
            dispositivo += $" — {part.Paragrafo}";
        }

        sb.Append(meta.Titulo);
        if (dispositivo is not null)
        {
            sb.Append(" — ").Append(dispositivo);
        }

        sb.Append('\n');
        if (!string.IsNullOrWhiteSpace(unit.Hierarquia))
        {
            sb.Append(unit.Hierarquia).Append('\n');
        }

        if (options.IncluirEmentaEmTodos && !string.IsNullOrWhiteSpace(meta.Ementa))
        {
            sb.Append("Ementa: ").Append(Trunc(meta.Ementa, options.EmentaMaxChars)).Append('\n');
        }

        sb.Append('\n');
        if (part.PrecisaCaput && caputLine is not null && !part.Linhas.Contains(caputLine))
        {
            sb.Append("(caput) ").Append(Trunc(caputLine.Trim(), 300)).Append("\n[...]\n");
        }

        sb.Append(string.Join('\n', part.Linhas).Trim());

        return CollapseBlank(sb.ToString());
    }

    private static string CollapseBlank(string text) => Regex.Replace(text, @"\n{3,}", "\n\n").Trim();

    private static string Trunc(string text, int max) => text.Length <= max ? text : text[..max].TrimEnd() + "…";

    private static string Hash(string text)
    {
        var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    [GeneratedRegex(@"^\s*Art(?:igo)?\.?\s*(?<n>\d+(?:\.\d{3})*(?:[ºo°])?(?:-[A-Za-z]{1,2})?)(?=[\s\.\-–—,:]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ArtigoRegex();

    [GeneratedRegex(@"^\s*(?<l>§\s*\d+[ºo°]?(?:-[A-Z])?|Par[áa]grafo [úu]nico)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ParagrafoRegex();

    [GeneratedRegex(@"^\s*(?<l>[IVXLCDM]{1,7})\s*[-–—)]\s", RegexOptions.CultureInvariant)]
    private static partial Regex IncisoRegex();

    [GeneratedRegex(@"^\s*[a-z]\)\s", RegexOptions.CultureInvariant)]
    private static partial Regex AlineaRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\W*(?:Art(?:igo)?\.?\s*[\w\.ºo°-]+\W*)?\W*\(?\s*\*?\(?\s*Revogad[oa]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SoRevogadoRegex();

    [GeneratedRegex(@"(?<=[\.\;\:])\s+(?=[A-ZÁÉÍÓÚÂÊÔÃÕÇ0-9§])", RegexOptions.CultureInvariant)]
    private static partial Regex FraseRegex();
}

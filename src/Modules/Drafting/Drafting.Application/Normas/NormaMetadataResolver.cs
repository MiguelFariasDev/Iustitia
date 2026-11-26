using System.Globalization;
using System.Text.RegularExpressions;
using Advocacia.Modules.Drafting.Domain.Normas;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>Resultado da resolução: metadados utilizáveis OU o motivo de o arquivo não servir.</summary>
public sealed record ResolucaoMetadata(NormaMetadata? Metadata, string? Motivo, IReadOnlyList<string> Avisos)
{
    public bool Ok => Metadata is not null;
}

/// <summary>
/// Transforma YAML + caminho + conteúdo em <see cref="NormaMetadata"/>. NÃO confia cegamente no YAML: campo
/// ausente ou inválido é completado por caminho/conteúdo; o que não dá para saber fica explícito
/// ("Não informada") em vez de inventado. Não depende de IO nem de relógio — testável com strings.
/// </summary>
public static partial class NormaMetadataResolver
{
    private static readonly Regex AnoNoTitulo = AnoNoTituloRegex();
    private static readonly Regex NumeroNoTitulo = NumeroNoTituloRegex();
    private static readonly Regex DataBr = DataBrRegex();
    private static readonly Regex PrimeiroH1 = PrimeiroH1Regex();
    private static readonly Regex FonteConstituicao = FonteConstituicaoRegex();
    private static readonly Regex Epigrafe = EpigrafeRegex();

    private static readonly Dictionary<string, string> TiposPorPasta = new(StringComparer.OrdinalIgnoreCase)
    {
        ["leis-ordinarias"] = "Lei Ordinária",
        ["leis-complementares"] = "Lei Complementar",
        ["decretos"] = "Decreto",
        ["decretos-lei"] = "Decreto-Lei",
        ["decretos-legislativos"] = "Decreto Legislativo",
        ["decretos-nao-numerados"] = "Decreto não numerado",
        ["medidas-provisorias"] = "Medida Provisória",
        ["resolucoes-senado"] = "Resolução do Senado",
        ["resolucoes-camara"] = "Resolução da Câmara",
        ["emendas-constitucionais"] = "Emenda Constitucional",
        ["sumulas"] = "Súmula",
        ["sumulas-vinculantes"] = "Súmula vinculante",
        ["repetitivos"] = "Tema repetitivo",
        ["repercussao-geral"] = "Tema de repercussão geral",
        ["controle-concentrado"] = "Controle concentrado",
        ["orientacoes-jurisprudenciais"] = "Orientação Jurisprudencial",
        ["uniformizacao"] = "Pedido de uniformização",
        ["regimentos"] = "Regimento interno",
    };

    public static ResolucaoMetadata Resolver(string repoPath, FrontMatter fm)
    {
        var avisos = new List<string>();
        var segmentos = repoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var raiz = segmentos.Length > 0 ? segmentos[0].ToLowerInvariant() : string.Empty;
        var corpo = fm.Corpo;

        if (raiz is not ("constituicao.md" or "federal" or "jurisprudencia"))
        {
            return new ResolucaoMetadata(null, "fora do escopo (só Constituição, leis federais e jurisprudência)", avisos);
        }

        if (corpo.Trim().Length < 20)
        {
            return new ResolucaoMetadata(null, "arquivo sem conteúdo textual", avisos);
        }

        var titulo = Limpar(fm.Get("titulo"))
            ?? Limpar(PrimeiroH1.Match(corpo) is { Success: true } h1 ? h1.Groups[1].Value : null)
            ?? Limpar(Path.GetFileNameWithoutExtension(repoPath));
        if (string.IsNullOrWhiteSpace(titulo))
        {
            return new ResolucaoMetadata(null, "sem título", avisos);
        }

        if (!fm.Presente && repoPath != "constituicao.md")
        {
            avisos.Add("Sem cabeçalho YAML: metadados deduzidos do caminho e do conteúdo.");
        }

        var tribunal = Limpar(fm.Get("tribunal")) ?? TribunalDoCaminho(segmentos);
        var tipo = Limpar(fm.Get("tipo")) ?? TipoDoCaminho(segmentos, raiz);
        var numero = Limpar(fm.Get("numero")) ?? (NumeroNoTitulo.Match(titulo) is { Success: true } n ? n.Groups[1].Value.Replace(".", string.Empty) : null);

        var data = ParseData(fm.Get("data")) ?? DataNoTitulo(titulo);
        var ano = data?.Year
            ?? (AnoNoTitulo.Match(titulo) is { Success: true } a && int.TryParse(a.Groups[1].Value, out var y) ? y : (int?)null)
            ?? AnoDoCaminho(segmentos);

        var revogada = ParseBool(fm.Get("revogada"));
        var situacao = Limpar(fm.Get("situacao")) ?? Limpar(fm.Get("situacao_indice"))
            ?? (revogada switch { true => "Revogada", false => "Vigente (conforme a fonte)", _ => "Não informada" });
        if (revogada is null && situacao.Contains("revogad", StringComparison.OrdinalIgnoreCase))
        {
            revogada = true;
        }

        var fonte = SomenteHttp(fm.Get("fonte")) ?? SomenteHttp(fm.Get("fonte_texto"))
            ?? (FonteConstituicao.Match(corpo) is { Success: true } f ? f.Groups[1].Value : null);
        if (fonte is null)
        {
            avisos.Add("Fonte oficial não identificada no arquivo.");
        }

        var esfera = raiz == "jurisprudencia" ? "jurisprudencia" : "federal";
        var uf = Limpar(fm.Get("uf"))?.ToUpperInvariant();
        var municipio = Limpar(fm.Get("municipio"));
        var area = Limpar(fm.Get("area")) is { } areaYaml ? AreaMapper.Normalizar(areaYaml) : AreaMapper.Deduzir(tribunal, titulo, repoPath);

        if (raiz == "constituicao.md")
        {
            // Sem YAML no arquivo: data de promulgação e texto compilado são fatos do próprio documento.
            data ??= new DateOnly(1988, 10, 5);
            ano = data.Value.Year;
            revogada = false;
            situacao = "Vigente (conforme a fonte)";
        }

        var metadata = new NormaMetadata(
            titulo,
            tipo,
            numero,
            ano,
            data,
            Limpar(fm.Get("publicacao")) ?? Limpar(fm.Get("data_publicacao")),
            Limpar(fm.Get("ementa")) ?? (raiz == "federal" ? EmentaDoCorpo(corpo) : null),
            situacao,
            revogada ?? false,
            Limpar(fm.Get("versao_texto")) ?? (raiz == "constituicao.md" ? "compilada (texto vigente)" : "Não informada"),
            Limpar(fm.Get("urn")),
            fonte ?? string.Empty,
            esfera,
            uf,
            municipio,
            tribunal,
            area,
            ParseLista(fm.Get("assuntos") ?? fm.Get("tags")),
            ColecaoDe(raiz));

        return new ResolucaoMetadata(metadata, null, avisos);
    }

    public static string ColecaoDe(string raiz) => raiz switch
    {
        "constituicao.md" => Colecoes.ConstituicaoFederal,
        "jurisprudencia" => Colecoes.Jurisprudencia,
        _ => Colecoes.LeisFederais,
    };

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? SomenteHttp(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.ToString() : null;

    private static bool? ParseBool(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch { "true" or "sim" or "yes" => true, "false" or "nao" or "não" or "no" => false, _ => null };

    private static DateOnly? ParseData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateOnly.TryParseExact(raw.Trim(), ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            && data.Year is >= 1500 and <= 2200
            ? data
            : null;
    }

    private static DateOnly? DataNoTitulo(string titulo)
    {
        var match = DataBr.Match(titulo);

        return match.Success ? ParseData(match.Value) : null;
    }

    private static int? AnoDoCaminho(string[] segmentos) =>
        segmentos.Select(s => int.TryParse(s, out var year) && year is >= 1500 and <= 2200 ? year : (int?)null).FirstOrDefault(y => y is not null);

    private static string? TribunalDoCaminho(string[] segmentos)
    {
        if (segmentos.Length < 2 || !segmentos[0].Equals("jurisprudencia", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // jurisprudencia/outros-tribunais/stm/... → STM; jurisprudencia/stf/... → STF; regimentos não têm tribunal no caminho.
        if (segmentos[1].Equals("regimentos", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidato = segmentos[1].Equals("outros-tribunais", StringComparison.OrdinalIgnoreCase) && segmentos.Length > 2
            ? segmentos[2]
            : segmentos[1];

        return candidato.ToUpperInvariant();
    }

    private static string TipoDoCaminho(string[] segmentos, string raiz)
    {
        if (raiz == "constituicao.md")
        {
            return "Constituição";
        }

        foreach (var segmento in segmentos.Reverse())
        {
            if (TiposPorPasta.TryGetValue(segmento, out var tipo))
            {
                return tipo;
            }
        }

        return raiz switch
        {
            "jurisprudencia" => "Decisão",
            _ => "Norma",
        };
    }

    /// <summary>Primeiro parágrafo curto depois do título que não seja a epígrafe nem um artigo.</summary>
    private static string? EmentaDoCorpo(string corpo)
    {
        foreach (var bloco in corpo.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Take(8))
        {
            var texto = bloco.Trim();
            if (texto.StartsWith('#') || texto.StartsWith("**Art", StringComparison.Ordinal) || texto.StartsWith("Art", StringComparison.Ordinal)
                || texto.StartsWith('>') || texto.StartsWith("```", StringComparison.Ordinal) || Epigrafe.IsMatch(texto))
            {
                continue;
            }

            if (texto.Length is >= 15 and <= 700 && !texto.StartsWith("O PRESIDENTE", StringComparison.OrdinalIgnoreCase))
            {
                return texto.Replace("\n", " ");
            }
        }

        return null;
    }

    /// <summary>"[a, b]" ou "a, b" → lista limpa e sem repetição.</summary>
    private static string[] ParseLista(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim('"', '\''))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToArray();
    }

    [GeneratedRegex(@"\b(1[5-9]\d{2}|2[01]\d{2})\b")]
    private static partial Regex AnoNoTituloRegex();

    [GeneratedRegex(@"n[ºo°.]*\s*([\d\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex NumeroNoTituloRegex();

    [GeneratedRegex(@"\b\d{2}/\d{2}/\d{4}\b")]
    private static partial Regex DataBrRegex();

    [GeneratedRegex(@"^#\s+(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex PrimeiroH1Regex();

    [GeneratedRegex(@">\s*Fonte:\s*\[[^\]]*\]\((https?://[^)\s]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex FonteConstituicaoRegex();

    [GeneratedRegex(@"^(LEI|DECRETO|EMENDA|RESOLU|MEDIDA|PORTARIA|INSTRU|S[ÚU]MULA|CONSTITUI)", RegexOptions.IgnoreCase)]
    private static partial Regex EpigrafeRegex();
}

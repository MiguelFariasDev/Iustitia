using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Normas;

public sealed record CandidatoBusca(VectorHit Hit, double Fusao);

public sealed record RerankContexto(string Consulta, string? Area, IReadOnlyList<string>? Ufs);

public sealed record CandidatoRerankeado(VectorHit Hit, double Score);

/// <summary>
/// Reranking por regras, determinístico e sem custo de GPU. Combina: similaridade (fusão vetor+palavra-chave),
/// correspondência de termos jurídicos, hierarquia normativa, autoridade da fonte, vigência, área, UF,
/// recência e presença do dispositivo citado na consulta.
///
/// NÃO é um cross-encoder: o Ollama não expõe endpoint de reranking, então um modelo de reranking exigiria
/// outro servidor. A interface <see cref="INormaReranker"/> existe para trocar por um quando houver.
/// Normas REVOGADAS não são descartadas (podem reger fatos antigos): só perdem pontos e são sinalizadas.
/// </summary>
public static class NormaReranker
{
    private static readonly string[] Hosts =
    [
        "planalto.gov.br", "normas.leg.br", "camara.leg.br", "senado.leg.br", "stf.jus.br", "stj.jus.br", "tst.jus.br",
        "tse.jus.br", "cnj.jus.br", "pdpj.jus.br", "jus.br", "gov.br",
    ];

    private static readonly HashSet<string> Superiores = new(StringComparer.OrdinalIgnoreCase) { "STF", "STJ", "TST", "TSE", "STM" };

    public static IReadOnlyList<CandidatoRerankeado> Reordenar(IReadOnlyList<CandidatoBusca> candidatos, RerankContexto contexto)
    {
        if (candidatos.Count == 0)
        {
            return [];
        }

        var maxFusao = candidatos.Max(c => c.Fusao);
        var termos = NormaTexto.Termos(contexto.Consulta);
        var artigos = NormaTexto.ArtigosMencionados(contexto.Consulta);
        var normas = NormaTexto.NormasMencionadas(contexto.Consulta);

        return candidatos
            .Select(c => new CandidatoRerankeado(c.Hit, Pontuar(c, maxFusao <= 0 ? 1 : maxFusao, termos, artigos, normas, contexto)))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Hit.Meta.Norma?.Titulo, StringComparer.Ordinal)
            .ThenBy(r => r.Hit.PointId, StringComparer.Ordinal)
            .ToList();
    }

    internal static double Pontuar(
        CandidatoBusca candidato, double maxFusao, IReadOnlyList<string> termos,
        IReadOnlySet<string> artigos, IReadOnlySet<string> normasCitadas, RerankContexto contexto)
    {
        var hit = candidato.Hit;
        var norma = hit.Meta.Norma;
        var sem = Math.Clamp(candidato.Fusao / maxFusao, 0, 1);

        var texto = hit.Texto.ToLowerInvariant();
        var lex = termos.Count == 0 ? 0 : (double)termos.Count(t => texto.Contains(t, StringComparison.Ordinal)) / termos.Count;

        var score = (0.50 * sem) + (0.25 * lex);
        if (norma is null)
        {
            return score;
        }

        score += 0.10 * Hierarquia(norma, hit.Meta.Tribunal);
        score += Autoridade(hit.Meta.UrlOrigem, norma, hit.Meta.Tribunal);

        if (norma.Revogada || norma.Situacao.Contains("revogad", StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.15;
        }

        var numeroConfere = norma.Numero is not null && normasCitadas.Contains(norma.Numero.Replace(".", string.Empty));
        if (numeroConfere)
        {
            score += 0.15;
        }

        var artigo = NormaTexto.NumeroDoArtigo(norma.Artigo);
        if (artigo is not null && artigos.Contains(artigo) && (normasCitadas.Count == 0 || numeroConfere))
        {
            score += 0.20;
        }

        if (!string.IsNullOrEmpty(contexto.Area) && !string.IsNullOrEmpty(hit.Meta.Area))
        {
            if (string.Equals(hit.Meta.Area, contexto.Area, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.08;
            }
            else if (!string.Equals(hit.Meta.Area, "constitucional", StringComparison.OrdinalIgnoreCase))
            {
                score -= 0.10;
            }
        }

        if (norma.Uf is not null && contexto.Ufs?.Contains(norma.Uf, StringComparer.OrdinalIgnoreCase) == true)
        {
            score += 0.05;
        }

        if (hit.Meta.DataPublicacao is { } data)
        {
            score += Math.Clamp((data.Year - 1990) / 40.0, 0, 1) * 0.03;
        }

        return score;
    }

    internal static double Hierarquia(NormaRef norma, string? tribunal)
    {
        var tipo = norma.Tipo.ToLowerInvariant();

        return tipo switch
        {
            _ when tipo.Contains("constitui", StringComparison.Ordinal) || tipo.Contains("emenda constitucional", StringComparison.Ordinal) => 1.0,
            _ when tipo.Contains("vinculante", StringComparison.Ordinal) => 1.0,
            _ when tipo.Contains("súmula", StringComparison.Ordinal) || tipo.Contains("sumula", StringComparison.Ordinal) =>
                tribunal is not null && Superiores.Contains(tribunal) ? 0.9 : 0.6,
            _ when tipo.Contains("repetitivo", StringComparison.Ordinal) || tipo.Contains("repercuss", StringComparison.Ordinal) => 0.9,
            _ when tipo is "adi" or "adc" or "adpf" or "ado" => 0.9,
            _ when tipo.Contains("lei complementar", StringComparison.Ordinal) => 0.9,
            _ when tipo.Contains("lei", StringComparison.Ordinal) || tipo.Contains("medida provis", StringComparison.Ordinal) => 0.85,
            _ when tipo.Contains("tratado", StringComparison.Ordinal) => 0.8,
            _ when tipo.Contains("decreto", StringComparison.Ordinal) => 0.6,
            _ when tipo.Contains("resolu", StringComparison.Ordinal) || tipo.Contains("portaria", StringComparison.Ordinal)
                || tipo.Contains("instru", StringComparison.Ordinal) => 0.45,
            _ => 0.4,
        };
    }

    internal static double Autoridade(string? fonteUrl, NormaRef norma, string? tribunal)
    {
        var bonus = 0.0;
        if (string.IsNullOrWhiteSpace(fonteUrl))
        {
            bonus -= 0.05;
        }
        else if (Uri.TryCreate(fonteUrl, UriKind.Absolute, out var uri)
            && Hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)))
        {
            bonus += 0.05;
        }

        if (norma.Esfera == "jurisprudencia" && tribunal is not null && Superiores.Contains(tribunal))
        {
            bonus += 0.05;
        }

        return bonus;
    }
}

public interface INormaReranker
{
    IReadOnlyList<CandidatoRerankeado> Reordenar(IReadOnlyList<CandidatoBusca> candidatos, RerankContexto contexto);
}

public sealed class RuleBasedNormaReranker : INormaReranker
{
    public IReadOnlyList<CandidatoRerankeado> Reordenar(IReadOnlyList<CandidatoBusca> candidatos, RerankContexto contexto) =>
        NormaReranker.Reordenar(candidatos, contexto);
}

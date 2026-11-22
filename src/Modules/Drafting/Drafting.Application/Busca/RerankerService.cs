using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Casos;

namespace Advocacia.Modules.Drafting.Application.Busca;

/// <param name="Consulta">Texto completo da consulta (tese, temas, fatos, pedidos): base da correspondência de termos.</param>
/// <param name="DataFatos">Norma posterior aos fatos perde pontos; revogada que vigia na época não perde tanto.</param>
public sealed record RerankContexto(
    string Consulta,
    string? Area,
    IReadOnlyList<string>? Ufs = null,
    string? Fatos = null,
    string? Pedidos = null,
    IReadOnlyList<string>? TribunaisPreferidos = null,
    IReadOnlyList<ReferenciaLegal>? Referencias = null,
    DateOnly? DataFatos = null,
    DateOnly? PeriodoDesde = null);

public sealed record CandidatoRerankeado(VectorHit Hit, double Score);

public interface IRerankerService
{
    IReadOnlyList<CandidatoRerankeado> Reordenar(IReadOnlyList<CandidatoBusca> candidatos, RerankContexto contexto);
}

/// <summary>
/// Reranking por regras, determinístico e sem custo de GPU. Combina: similaridade (fusão vetor + BM25 +
/// referência exata), correspondência de termos jurídicos, compatibilidade com os fatos e com os pedidos,
/// hierarquia normativa, autoridade da fonte, vigência, área, UF, tribunal preferido, recência e presença do
/// dispositivo citado.
///
/// NÃO é um cross-encoder: o Ollama não expõe endpoint de reranking, então um modelo exigiria outro servidor.
/// A interface existe para trocar por um quando houver. Norma REVOGADA não é descartada (pode reger fato
/// antigo): perde pontos e é sinalizada.
/// </summary>
public sealed class RerankerService : IRerankerService
{
    private static readonly string[] HostsOficiais =
    [
        "planalto.gov.br", "normas.leg.br", "camara.leg.br", "senado.leg.br", "stf.jus.br", "stj.jus.br", "tst.jus.br",
        "tse.jus.br", "cnj.jus.br", "pdpj.jus.br", "jus.br", "gov.br",
    ];

    private static readonly HashSet<string> Superiores = new(StringComparer.OrdinalIgnoreCase) { "STF", "STJ", "TST", "TSE", "STM" };

    public IReadOnlyList<CandidatoRerankeado> Reordenar(IReadOnlyList<CandidatoBusca> candidatos, RerankContexto contexto)
    {
        if (candidatos.Count == 0)
        {
            return [];
        }

        var maxFusao = Math.Max(candidatos.Max(c => c.Fusao), double.Epsilon);
        var sinais = new Sinais(
            Termos(contexto.Consulta, 24),
            Termos(contexto.Fatos, 24),
            Termos(contexto.Pedidos, 16),
            contexto.Referencias ?? ExtratorReferencias.Extrair(contexto.Consulta));

        return candidatos
            .Select(c => new CandidatoRerankeado(c.Hit, Pontuar(c, maxFusao, sinais, contexto)))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Hit.Meta.Norma?.Titulo, StringComparer.Ordinal)
            .ThenBy(r => r.Hit.PointId, StringComparer.Ordinal)
            .ToList();
    }

    private sealed record Sinais(
        IReadOnlySet<string> Consulta, IReadOnlySet<string> Fatos, IReadOnlySet<string> Pedidos, IReadOnlyList<ReferenciaLegal> Referencias);

    private static double Pontuar(CandidatoBusca candidato, double maxFusao, Sinais sinais, RerankContexto contexto)
    {
        var hit = candidato.Hit;
        var termosDoTrecho = Bm25.Tokenizar(hit.Texto).ToHashSet(StringComparer.Ordinal);

        var score = (0.45 * Math.Clamp(candidato.Fusao / maxFusao, 0, 1))
            + (0.15 * Cobertura(sinais.Consulta, termosDoTrecho))
            + (0.10 * Cobertura(sinais.Fatos, termosDoTrecho))
            + (0.10 * Cobertura(sinais.Pedidos, termosDoTrecho));

        if (hit.Meta.Norma is not { } norma)
        {
            return score + AjusteDoAnexo(hit.Meta.Caso);
        }

        score += 0.10 * Hierarquia(norma, hit.Meta.Tribunal);
        score += Autoridade(hit.Meta.UrlOrigem, norma, hit.Meta.Tribunal);
        score += Vigencia(norma, hit.Meta.DataPublicacao, contexto.DataFatos);
        score += DispositivoCitado(norma, hit.Meta, sinais.Referencias);
        score += AfinidadeDeArea(hit.Meta.Area, contexto.Area);
        if (norma.Hierarquia?.StartsWith("ATO DAS", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Disposições transitórias: específicas e datadas, quase nunca são o fundamento procurado.
            score -= 0.20;
        }


        if (norma.Uf is not null && contexto.Ufs?.Contains(norma.Uf, StringComparer.OrdinalIgnoreCase) == true)
        {
            score += 0.05;
        }

        if (hit.Meta.Tribunal is { } tribunal
            && contexto.TribunaisPreferidos?.Contains(tribunal, StringComparer.OrdinalIgnoreCase) == true)
        {
            score += 0.05;
        }

        if (hit.Meta.DataPublicacao is { } data)
        {
            // Recência desempata; não decide.
            score += Math.Clamp((data.Year - 1990) / 40.0, 0, 1) * 0.03;
            if (norma.Colecao == Colecoes.Jurisprudencia && contexto.PeriodoDesde is { } desde && data >= desde)
            {
                score += 0.03;
            }
        }

        return score;
    }

    /// <summary>Leitura óptica fraca e transcrição valem menos que texto nativo; trecho suspeito vai para o fim.</summary>
    private static double AjusteDoAnexo(CasoRef? caso)
    {
        if (caso is null)
        {
            return 0;
        }

        if (caso.Suspeito)
        {
            return -1;
        }

        return caso.Origem switch
        {
            OrigemTexto.Ocr when (caso.Confianca ?? 0) < CasoDocumento.ConfiancaOcrBaixa => -0.08,
            OrigemTexto.Transcricao => -0.05,
            _ => 0,
        };
    }

    internal static double Hierarquia(NormaRef norma, string? tribunal)
    {
        var tipo = norma.Tipo.ToLowerInvariant();

        return tipo switch
        {
            _ when tipo.Contains("constitui", StringComparison.Ordinal) => 1.0,
            _ when tipo.Contains("vinculante", StringComparison.Ordinal) => 1.0,
            _ when tipo.Contains("súmula", StringComparison.Ordinal) || tipo.Contains("sumula", StringComparison.Ordinal) =>
                tribunal is not null && Superiores.Contains(tribunal) ? 0.9 : 0.6,
            _ when tipo.Contains("repetitivo", StringComparison.Ordinal) || tipo.Contains("repercuss", StringComparison.Ordinal) => 0.95,
            _ when tipo is "adi" or "adc" or "adpf" or "ado" => 0.95,
            _ when tipo.Contains("orientação jurisprudencial", StringComparison.Ordinal) => 0.75,
            _ when tipo.Contains("lei complementar", StringComparison.Ordinal) => 0.9,
            _ when tipo.Contains("decreto-lei", StringComparison.Ordinal) => 0.85,
            _ when tipo.Contains("decreto", StringComparison.Ordinal) => 0.6,
            _ when tipo.Contains("lei", StringComparison.Ordinal) || tipo.Contains("medida provis", StringComparison.Ordinal) => 0.85,
            _ when tipo.Contains("resolu", StringComparison.Ordinal) || tipo.Contains("portaria", StringComparison.Ordinal) => 0.45,
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
            && HostsOficiais.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)))
        {
            bonus += 0.05;
        }

        if (norma.Esfera == "jurisprudencia" && tribunal is not null && Superiores.Contains(tribunal))
        {
            bonus += 0.05;
        }

        return bonus;
    }

    internal static double Vigencia(NormaRef norma, DateOnly? dataNorma, DateOnly? dataFatos)
    {
        if (norma.ForaDeVigor())
        {
            // Fato anterior: a norma hoje revogada pode ser a que regia o caso.
            return dataFatos is { } fatos && dataNorma is { } data && data <= fatos ? -0.05 : -0.15;
        }

        return dataFatos is { } f && dataNorma is { } d && d > f && norma.Colecao != Colecoes.Jurisprudencia ? -0.08 : 0;
    }

    private static double DispositivoCitado(NormaRef norma, ChunkMetadata meta, IReadOnlyList<ReferenciaLegal> referencias)
    {
        var melhor = 0.0;
        foreach (var referencia in referencias)
        {
            if (!MesmaNorma(norma, meta, referencia))
            {
                continue;
            }

            var artigoConfere = referencia.Artigo is not null && norma.Artigo is not null
                && ExtratorReferencias.NormalizarArtigo(norma.Artigo) == referencia.Artigo;
            melhor = Math.Max(melhor, referencia.Tipo != TipoReferencia.Norma || artigoConfere ? 0.35 : 0.10);
        }

        return melhor;
    }

    internal static bool MesmaNorma(NormaRef norma, ChunkMetadata meta, ReferenciaLegal referencia)
    {
        var tipo = norma.Tipo.ToLowerInvariant();
        if (referencia.Constituicao)
        {
            return norma.Colecao == Colecoes.ConstituicaoFederal;
        }

        if (norma.Numero is null || norma.Numero.Replace(".", string.Empty) != referencia.Numero)
        {
            return false;
        }

        var tribunalConfere = referencia.Tribunal is null
            || string.Equals(meta.Tribunal, referencia.Tribunal, StringComparison.OrdinalIgnoreCase);

        return referencia.Tipo switch
        {
            TipoReferencia.Sumula => (tipo.Contains("súmula", StringComparison.Ordinal) || tipo.Contains("sumula", StringComparison.Ordinal))
                && tribunalConfere && referencia.Vinculante == tipo.Contains("vinculante", StringComparison.Ordinal),
            TipoReferencia.Tema => (tipo.Contains("tema", StringComparison.Ordinal) || tipo.Contains("repercuss", StringComparison.Ordinal)) && tribunalConfere,
            _ => norma.Colecao == Colecoes.LeisFederais,
        };
    }

    private static double AfinidadeDeArea(string? areaDoTrecho, string? areaDoCaso)
    {
        if (string.IsNullOrEmpty(areaDoTrecho) || string.IsNullOrEmpty(areaDoCaso))
        {
            return 0;
        }

        if (string.Equals(areaDoTrecho, areaDoCaso, StringComparison.OrdinalIgnoreCase))
        {
            return 0.08;
        }

        return AreaMapper.Compativeis(areaDoCaso).Contains(areaDoTrecho, StringComparer.OrdinalIgnoreCase) ? 0.02 : -0.10;
    }

    private static IReadOnlySet<string> Termos(string? texto, int max) =>
        string.IsNullOrWhiteSpace(texto)
            ? new HashSet<string>()
            : Bm25.Tokenizar(texto)
                .Where(t => t.Length >= 4 && !char.IsDigit(t[0]))
                .GroupBy(t => t, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key.Length)
                .Take(max)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.Ordinal);

    private static double Cobertura(IReadOnlySet<string> termos, HashSet<string> trecho) =>
        termos.Count == 0 ? 0 : (double)termos.Count(trecho.Contains) / termos.Count;
}

public static class NormaRefExtensions
{
    /// <summary>Revogada, cancelada ou superada, conforme a fonte (que pode marcar só na situação).</summary>
    public static bool ForaDeVigor(this NormaRef norma) =>
        norma.Revogada
        || norma.Situacao.Contains("revogad", StringComparison.OrdinalIgnoreCase)
        || norma.Situacao.Contains("cancelad", StringComparison.OrdinalIgnoreCase)
        || norma.Situacao.Contains("superad", StringComparison.OrdinalIgnoreCase);
}

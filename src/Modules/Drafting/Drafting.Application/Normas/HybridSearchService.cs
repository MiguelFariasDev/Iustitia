using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Busca híbrida obrigatória: filtros estruturados + palavra-chave (full-text do PostgreSQL) + vetor (Qdrant),
/// fundidos por RRF, deduplicados, reranqueados e limitados (5–10). A falha da palavra-chave degrada para
/// só-vetor com aviso; a falha do vetor propaga (sem semântica a recuperação deixa de ser confiável).
/// </summary>
public sealed class HybridSearchService(
    INormaVectorStore vectors,
    INormaSearchIndex keywords,
    INormaReranker reranker,
    IOptions<BuscaNormasOptions> options,
    ILogger<HybridSearchService> logger) : IHybridSearchService
{
    /// <summary>Trechos por norma no resultado final: uma lei grande não pode monopolizar o contexto.</summary>
    private const int MaxPorNorma = 3;

    public async Task<IReadOnlyList<VectorHit>> BuscarAsync(
        NormaBusca busca, float[] vetor, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var filtro = busca.Filtro with { IncluirRevogadas = settings.IncluirRevogadas && busca.Filtro.IncluirRevogadas };

        var vectorTask = vectors.SearchAsync(filtro, vetor, settings.CandidatosVetor, cancellationToken);
        var keywordTask = SafeKeywordsAsync(filtro, busca.TextoConsulta, settings.CandidatosPalavraChave, cancellationToken);
        await Task.WhenAll(vectorTask, keywordTask);

        var candidatos = Fundir(vectorTask.Result, keywordTask.Result, settings.RrfK);
        var ordenados = reranker.Reordenar(
            candidatos,
            new RerankContexto(busca.TextoConsulta, filtro.Area, filtro.Ufs));

        var final = Selecionar(ordenados, settings.ResultadosMax);
        logger.LogInformation(
            "Busca híbrida: {Vetor} vetoriais, {Palavra} por palavra-chave, {Fundidos} fundidos, {Final} no resultado.",
            vectorTask.Result.Count, keywordTask.Result.Count, candidatos.Count, final.Count);

        return final;
    }

    private async Task<IReadOnlyList<VectorHit>> SafeKeywordsAsync(
        NormaFiltro filtro, string consulta, int limite, CancellationToken cancellationToken)
    {
        try
        {
            return await keywords.BuscarPalavrasChaveAsync(filtro, consulta, limite, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Busca por palavra-chave indisponível; seguindo só com a busca vetorial.");

            return [];
        }
    }

    /// <summary>
    /// Reciprocal Rank Fusion: score = Σ 1/(k + posição). Não depende da escala dos scores (cosseno × ts_rank),
    /// que não são comparáveis. Preferimos o hit do vetor (payload completo) quando o trecho vem das duas buscas.
    /// </summary>
    internal static IReadOnlyList<CandidatoBusca> Fundir(
        IReadOnlyList<VectorHit> vetoriais, IReadOnlyList<VectorHit> palavras, int k)
    {
        var porId = new Dictionary<string, (VectorHit Hit, double Score)>(StringComparer.Ordinal);

        void Somar(IReadOnlyList<VectorHit> lista)
        {
            for (var posicao = 0; posicao < lista.Count; posicao++)
            {
                var hit = lista[posicao];
                var parcela = 1.0 / (k + posicao + 1);
                porId[hit.PointId] = porId.TryGetValue(hit.PointId, out var atual)
                    ? (atual.Hit, atual.Score + parcela)
                    : (hit, parcela);
            }
        }

        Somar(vetoriais);
        Somar(palavras);

        return porId.Values
            .OrderByDescending(v => v.Score)
            .Select(v => new CandidatoBusca(v.Hit, v.Score))
            .ToList();
    }

    /// <summary>Remove duplicatas por conteúdo, limita trechos por norma e corta no máximo.</summary>
    internal static IReadOnlyList<VectorHit> Selecionar(IReadOnlyList<CandidatoRerankeado> ordenados, int max)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var porNorma = new Dictionary<Guid, int>();
        var resultado = new List<VectorHit>();

        foreach (var candidato in ordenados)
        {
            if (resultado.Count >= max)
            {
                break;
            }

            var hit = candidato.Hit;
            if (!vistos.Add(ContextSelector.ContentHash(hit)))
            {
                continue;
            }

            if (hit.Meta.Norma is { } norma)
            {
                var usados = porNorma.GetValueOrDefault(norma.NormaId);
                if (usados >= MaxPorNorma)
                {
                    continue;
                }

                porNorma[norma.NormaId] = usados + 1;
            }

            // O score que segue adiante é o do reranking: é a ordem de relevância final.
            resultado.Add(hit with { Score = candidato.Score });
        }

        return resultado;
    }
}

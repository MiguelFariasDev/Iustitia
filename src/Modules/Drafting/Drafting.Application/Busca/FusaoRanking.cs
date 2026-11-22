using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Busca;

/// <summary>Resultado de UMA busca (vetorial, BM25, referência exata), já na ordem de relevância dela.</summary>
public sealed record ListaRankeada(string Nome, IReadOnlyList<VectorHit> Hits, double Peso = 1.0);

public sealed record CandidatoBusca(VectorHit Hit, double Fusao);

public enum MetodoFusao
{
    /// <summary>Reciprocal Rank Fusion: só olha posições, imune à escala dos scores.</summary>
    Rrf = 0,

    /// <summary>Distribution-Based Score Fusion: normaliza cada lista por média ± 3 desvios e soma.</summary>
    Dbsf = 1,
}

/// <summary>Fusão de rankings cujos scores não são comparáveis (cosseno × BM25 × correspondência exata).</summary>
public static class FusaoRanking
{
    public static IReadOnlyList<CandidatoBusca> Fundir(MetodoFusao metodo, IReadOnlyList<ListaRankeada> listas, int rrfK) =>
        metodo == MetodoFusao.Dbsf ? Dbsf(listas) : Rrf(listas, rrfK);

    /// <summary>score = Σ peso / (k + posição).</summary>
    public static IReadOnlyList<CandidatoBusca> Rrf(IReadOnlyList<ListaRankeada> listas, int k)
    {
        var acumulado = new Acumulador();
        foreach (var lista in listas)
        {
            for (var posicao = 0; posicao < lista.Hits.Count; posicao++)
            {
                acumulado.Somar(lista.Hits[posicao], lista.Peso / (k + posicao + 1));
            }
        }

        return acumulado.Ordenado();
    }

    public static IReadOnlyList<CandidatoBusca> Dbsf(IReadOnlyList<ListaRankeada> listas)
    {
        var acumulado = new Acumulador();
        foreach (var lista in listas.Where(l => l.Hits.Count > 0))
        {
            var media = lista.Hits.Average(h => h.Score);
            var desvio = Math.Sqrt(lista.Hits.Average(h => Math.Pow(h.Score - media, 2)));
            foreach (var hit in lista.Hits)
            {
                // Sem dispersão (um só resultado, ou todos iguais), a lista não diz nada sobre ordem: meio da escala.
                var normalizado = desvio < 1e-9 ? 0.5 : Math.Clamp((hit.Score - (media - (3 * desvio))) / (6 * desvio), 0, 1);
                acumulado.Somar(hit, normalizado * lista.Peso);
            }
        }

        return acumulado.Ordenado();
    }

    private sealed class Acumulador
    {
        private readonly Dictionary<string, (VectorHit Hit, double Score)> _porId = new(StringComparer.Ordinal);

        public void Somar(VectorHit hit, double parcela) =>
            _porId[hit.PointId] = _porId.TryGetValue(hit.PointId, out var atual)
                ? (atual.Hit, atual.Score + parcela)
                : (hit, parcela);

        public IReadOnlyList<CandidatoBusca> Ordenado() =>
            _porId.Values
                .OrderByDescending(v => v.Score)
                .ThenBy(v => v.Hit.PointId, StringComparer.Ordinal)
                .Select(v => new CandidatoBusca(v.Hit, v.Score))
                .ToList();
    }
}

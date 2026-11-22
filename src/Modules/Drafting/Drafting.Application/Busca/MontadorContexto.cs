using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;

namespace Advocacia.Modules.Drafting.Application.Busca;

/// <summary>
/// Escolhe os 5–10 trechos que vão ao modelo. Regras puras, sem rede: cota por grupo (anexos do caso têm
/// prioridade; Constituição, leis e jurisprudência têm vaga garantida quando há resultado relevante), sobra distribuída
/// por relevância, e tudo dentro do orçamento de tokens — o que não cabe é contado, nunca cortado no meio.
/// </summary>
public static class MontadorContexto
{
    /// <summary>Margem por trecho: rótulo, situação, versão e link da fonte que acompanham o texto no prompt.</summary>
    public const int TokensDeCabecalho = 60;

    public static RagContext Montar(
        IReadOnlyList<VectorHit> caso, IReadOnlyList<VectorHit> juridico, BuscaNormasOptions opcoes, int maxTokens, double charsPorToken)
    {
        var max = Math.Clamp(opcoes.ResultadosMax, 5, 10);
        var elegiveis = caso.Where(h => h.Meta.Caso is { Suspeito: false }).ToList();
        var grupos = new Dictionary<GrupoContexto, Queue<VectorHit>>
        {
            [GrupoContexto.DocumentosDoCaso] = new(elegiveis.OrderByDescending(h => h.Score)),
            [GrupoContexto.Constituicao] = Fila(juridico, GrupoContexto.Constituicao),
            [GrupoContexto.LeisFederais] = Fila(juridico, GrupoContexto.LeisFederais),
            [GrupoContexto.Jurisprudencia] = Fila(juridico, GrupoContexto.Jurisprudencia),
        };
        var vagas = new Dictionary<GrupoContexto, int>
        {
            [GrupoContexto.DocumentosDoCaso] = opcoes.VagasCaso,
            [GrupoContexto.Constituicao] = opcoes.VagasConstituicao,
            [GrupoContexto.LeisFederais] = opcoes.VagasLeis,
            [GrupoContexto.Jurisprudencia] = opcoes.VagasJurisprudencia,
        };

        // Ordem de disputa: rodadas, uma vaga por grupo a cada rodada, anexos primeiro. Com orçamento curto
        // isso garante um pouco de cada fonte em vez de quatro leis e nenhum fato.
        var ordem = new[]
        {
            GrupoContexto.DocumentosDoCaso, GrupoContexto.LeisFederais, GrupoContexto.Jurisprudencia, GrupoContexto.Constituicao,
        };
        var escolhidos = new List<RetrievedChunk>();
        var usados = new Dictionary<GrupoContexto, int>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var tokens = 0;
        var cortados = 0;
        var descartados = caso.Count - elegiveis.Count;

        var corte = juridico.Count == 0 ? 0 : juridico.Max(h => h.Score) * opcoes.RelevanciaMinimaReserva;

        bool MereceReserva(GrupoContexto grupo) =>
            grupo == GrupoContexto.DocumentosDoCaso || (grupos[grupo].TryPeek(out var proximo) && proximo.Score >= corte);

        bool Tentar(GrupoContexto grupo)
        {
            while (grupos[grupo].TryDequeue(out var hit))
            {
                if (!vistos.Add(BuscaJuridicaService.ChaveDeConteudo(hit)))
                {
                    descartados++;
                    continue;
                }

                var custo = TokenEstimator.Estimate(hit.Texto, charsPorToken) + TokensDeCabecalho;
                if (tokens + custo > maxTokens)
                {
                    cortados++;
                    continue;
                }

                tokens += custo;
                usados[grupo] = usados.GetValueOrDefault(grupo) + 1;
                escolhidos.Add(new RetrievedChunk(hit.PointId, hit.Texto, hit.Score, hit.Meta));

                return true;
            }

            return false;
        }

        for (var rodada = 0; rodada < max && escolhidos.Count < max; rodada++)
        {
            foreach (var grupo in ordem)
            {
                if (escolhidos.Count < max && usados.GetValueOrDefault(grupo) < vagas[grupo] && MereceReserva(grupo))
                {
                    Tentar(grupo);
                }
            }
        }

        // Vagas que sobraram vão para o melhor resultado disponível, de qualquer grupo.
        while (escolhidos.Count < max)
        {
            var proximo = grupos.Where(g => g.Value.Count > 0).OrderByDescending(g => g.Value.Peek().Score).Select(g => (GrupoContexto?)g.Key).FirstOrDefault();
            if (proximo is not { } grupo)
            {
                break;
            }

            Tentar(grupo);
        }

        var ordenados = escolhidos
            .OrderBy(c => RagContext.GrupoDe(c.Meta))
            .ThenByDescending(c => c.Score)
            .ToList();

        return new RagContext(ordenados, tokens, descartados, cortados);
    }

    private static Queue<VectorHit> Fila(IReadOnlyList<VectorHit> hits, GrupoContexto grupo) =>
        new(hits.Where(h => h.Meta.Norma is not null && RagContext.GrupoDe(h.Meta) == grupo).OrderByDescending(h => h.Score));
}

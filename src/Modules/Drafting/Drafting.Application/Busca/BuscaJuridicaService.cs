using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Busca;

/// <summary>
/// Busca híbrida OBRIGATÓRIA na base jurídica: para cada coleção pública, vetor (BGE-M3) + BM25 (Qdrant,
/// esparso com IDF); mais a referência exata (PostgreSQL). Filtros de vigência, área e tipo em todas.
/// Fusão por RRF ou DBSF, dedupe por conteúdo, reranking e limite por norma.
///
/// BM25 fora do ar degrada para o full-text do PostgreSQL; referência exata fora do ar só perde esse sinal;
/// vetor fora do ar PROPAGA (sem semântica a recuperação deixa de ser confiável, e a geração é repetida).
/// </summary>
public sealed class BuscaJuridicaService(
    INormaVectorStore vetores,
    INormaSearchIndex indice,
    IRerankerService reranker,
    IOptions<BuscaNormasOptions> options,
    ILogger<BuscaJuridicaService> logger) : IBuscaJuridicaService, IDisposable
{
    private readonly SemaphoreSlim _reserva = new(1, 1);

    public void Dispose() => _reserva.Dispose();

    public async Task<IReadOnlyList<VectorHit>> BuscarAsync(
        ConsultaPreparada consulta, CancellationToken cancellationToken = default)
    {
        var opcoes = options.Value;
        var filtro = new NormaFiltro(
            Areas: AreaMapper.Compativeis(consulta.Estruturada.Area),
            Ufs: consulta.Uf is null ? null : [consulta.Uf],
            IncluirRevogadas: opcoes.IncluirRevogadas);
        var esparso = Bm25.Consulta(consulta.TextoBm25);

        var porColecao = await Task.WhenAll(Colecoes.Publicas.Select(colecao =>
            BuscarColecaoAsync(colecao, filtro, consulta, esparso, opcoes, cancellationToken)));
        var exatas = await SeguroAsync(
            "referência exata",
            () => indice.BuscarReferenciasAsync(consulta.Referencias, opcoes.CandidatosBm25, cancellationToken));

        // Cada coleção é fundida em separado: uma coleção enorme não pode empurrar as outras para fora da lista.
        var candidatos = new List<CandidatoBusca>();
        foreach (var (colecao, listas) in Colecoes.Publicas.Zip(porColecao))
        {
            var daColecao = exatas.Where(h => h.Meta.Norma?.Colecao == colecao).ToList();
            candidatos.AddRange(FusaoRanking
                .Fundir(opcoes.Fusao, [.. listas, new ListaRankeada("referencia", daColecao, opcoes.PesoReferencia)], opcoes.RrfK)
                .Take(Math.Max(10, opcoes.CandidatosRerank / Colecoes.Publicas.Count)));
        }

        var ordenados = reranker.Reordenar(candidatos, Contexto(consulta));
        var final = Deduplicar(ordenados, opcoes.MaxPorNorma, opcoes.ResultadosMax * 3);

        // Só contagens: consulta e trechos nunca vão para o log.
        logger.LogInformation(
            "Busca jurídica: {Candidatos} candidatos, {Exatas} por referência exata, {Final} após reranking.",
            candidatos.Count, exatas.Count, final.Count);

        return final;
    }

    internal static RerankContexto Contexto(ConsultaPreparada consulta)
    {
        var e = consulta.Estruturada;

        return new RerankContexto(
            string.Join(' ', new[] { e.Tese, string.Join(' ', e.Temas), e.Pedidos }),
            e.Area,
            consulta.Uf is null ? null : [consulta.Uf],
            e.Fatos,
            e.Pedidos,
            e.TribunalPreferido,
            consulta.Referencias,
            consulta.DataFatos,
            PeriodoDesde(e.Periodo));
    }

    /// <summary>"últimos 5 anos" ou "desde 2020" → data inicial. Outra coisa: sem preferência de período.</summary>
    internal static DateOnly? PeriodoDesde(string? periodo, DateOnly? hoje = null)
    {
        if (string.IsNullOrWhiteSpace(periodo))
        {
            return null;
        }

        var numero = new string(periodo.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(numero, out var valor))
        {
            return null;
        }

        var referencia = hoje ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return valor switch
        {
            > 0 and <= 50 when periodo.Contains("ano", StringComparison.OrdinalIgnoreCase) => referencia.AddYears(-valor),
            >= 1900 and <= 2200 => new DateOnly(valor, 1, 1),
            _ => null,
        };
    }

    private async Task<IReadOnlyList<ListaRankeada>> BuscarColecaoAsync(
        string colecao, NormaFiltro filtro, ConsultaPreparada consulta, VetorEsparso esparso, BuscaNormasOptions opcoes,
        CancellationToken ct)
    {
        var densas = consulta.Vetores
            .Select(vetor => vetores.BuscarDensoAsync(colecao, filtro, vetor, opcoes.CandidatosVetor, ct))
            .ToList();
        var bm25 = esparso.Vazio
            ? Task.FromResult<IReadOnlyList<VectorHit>>([])
            : Bm25OuReservaAsync(colecao, filtro, consulta.TextoBm25, esparso, opcoes.CandidatosBm25, ct);

        var listas = new List<ListaRankeada>();
        foreach (var densa in await Task.WhenAll(densas))
        {
            // Vários vetores de consulta dividem o peso de "uma" busca semântica.
            listas.Add(new ListaRankeada("vetor", densa, 1.0 / Math.Max(1, densas.Count)));
        }

        listas.Add(new ListaRankeada("bm25", await bm25));

        return listas;
    }

    private async Task<IReadOnlyList<VectorHit>> Bm25OuReservaAsync(
        string colecao, NormaFiltro filtro, string texto, VetorEsparso esparso, int limite, CancellationToken ct)
    {
        try
        {
            return await vetores.BuscarBm25Async(colecao, filtro, esparso, limite, ct);
        }
        catch (VectorStoreException ex)
        {
            logger.LogWarning(ex, "BM25 indisponível em {Colecao}; usando o full-text do PostgreSQL.", colecao);

            // As coleções são consultadas em paralelo, mas o banco relacional não aceita duas consultas no mesmo contexto.
            await _reserva.WaitAsync(ct);
            try
            {
                return await SeguroAsync(
                    "full-text", () => indice.BuscarPalavrasChaveAsync(filtro with { Colecoes = [colecao] }, texto, limite, ct));
            }
            finally
            {
                _reserva.Release();
            }
        }
    }

    private async Task<IReadOnlyList<VectorHit>> SeguroAsync(string nome, Func<Task<IReadOnlyList<VectorHit>>> busca)
    {
        try
        {
            return await busca();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Busca por {Nome} indisponível; seguindo sem esse sinal.", nome);

            return [];
        }
    }

    /// <summary>Um trecho por conteúdo e no máximo N por norma. O score que segue é o do reranking.</summary>
    internal static IReadOnlyList<VectorHit> Deduplicar(
        IReadOnlyList<CandidatoRerankeado> ordenados, int maxPorNorma, int max)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var porNorma = new Dictionary<Guid, int>();
        var resultado = new List<VectorHit>();

        foreach (var (hit, score) in ordenados)
        {
            if (resultado.Count >= max)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(hit.Texto) || !vistos.Add(ChaveDeConteudo(hit)))
            {
                continue;
            }

            if (hit.Meta.Norma is { } norma)
            {
                var usados = porNorma.GetValueOrDefault(norma.NormaId);
                if (usados >= maxPorNorma)
                {
                    continue;
                }

                porNorma[norma.NormaId] = usados + 1;
            }

            resultado.Add(hit with { Score = score });
        }

        return resultado;
    }

    internal static string ChaveDeConteudo(VectorHit hit) =>
        string.IsNullOrWhiteSpace(hit.Meta.HashConteudo) ? TextoHash.DoTexto(hit.Texto) : hit.Meta.HashConteudo;
}

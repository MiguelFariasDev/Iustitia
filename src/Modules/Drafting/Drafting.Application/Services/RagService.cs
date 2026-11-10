using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Busca vetorial + montagem do contexto: embedding da consulta (BGE-M3) → busca no banco vetorial
/// filtrada por tenant e área → defesa em profundidade contra hits fora do filtro → dedupe por
/// hash → limite de trechos e de tokens.
/// </summary>
public sealed class RagService(
    IEmbeddingService embeddings,
    IVectorStore vectorStore,
    IOptions<RagOptions> options,
    ILogger<RagService> logger,
    IHybridSearchService? normas = null) : IRagService
{
    /// <summary>Quanto da consulta vira texto de embedding: o começo dos fatos já carrega o assunto.</summary>
    private const int ConsultaMaxChars = 2000;

    /// <summary>Bônus aos trechos de documentos escolhidos pelo usuário, para vencerem normas de score parecido.</summary>
    private const double ClientePriorizadoBonus = 0.5;

    public async Task<RagContext> RetrieveAsync(RagQuery query, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var limit = settings.LimiteEfetivo;

        var queryText = BuildQueryText(query);
        var vectors = await embeddings.EmbedAsync([queryText], cancellationToken);
        if (vectors.Count != 1)
        {
            throw new InvalidOperationException("O serviço de embeddings devolveu um número inesperado de vetores.");
        }

        // Pede o dobro: duplicatas e hits descartados não podem deixar o contexto abaixo do mínimo.
        var hits = await vectorStore.SearchAsync(
            new VectorSearchQuery(
                query.TenantId,
                vectors[0],
                limit * 2,
                Area: query.Area,
                DocumentosIds: query.DocumentosIds.Count == 0 ? null : query.DocumentosIds,
                ScoreMinimo: settings.ScoreMinimo,
                UsuarioId: query.UsuarioId),
            cancellationToken);

        var inScope = hits.Where(hit => IsInScope(hit, query)).ToList();

        // Acervo normativo público (leis, jurisprudência…): busca híbrida + reranking. Ausente em testes/dev sem acervo.
        IReadOnlyList<VectorHit> normHits = normas is null
            ? []
            : await normas.BuscarAsync(
                new NormaBusca(queryText, new NormaFiltro(Area: string.IsNullOrWhiteSpace(query.Area) ? null : query.Area)),
                vectors[0],
                cancellationToken);

        RagContext context;
        if (normHits.Count == 0)
        {
            context = ContextSelector.Select(inScope, limit, settings.MaxTokensContexto, settings.CharsPorToken, settings.ScoreMinimo);
        }
        else
        {
            // Os scores dos dois lados não são comparáveis (cosseno × reranking): documentos que o usuário
            // escolheu levam prioridade explícita; os demais do escritório entram pelo próprio score.
            var prioridade = query.DocumentosIds.Count > 0 ? ClientePriorizadoBonus : 0;
            var cliente = inScope
                .Where(hit => settings.ScoreMinimo is not { } minimo || hit.Score >= minimo)
                .Select(hit => hit with { Score = hit.Score + prioridade });
            context = ContextSelector.Select(
                cliente.Concat(normHits), limit, settings.MaxTokensContexto, settings.CharsPorToken);
        }

        // Só contagens: o texto dos trechos e da consulta nunca vai para o log.
        logger.LogInformation(
            "RAG: {Recuperados} hits do escritório, {Fora} fora do escopo, {Normas} do acervo normativo, {Escolhidos} no contexto (~{Tokens} tokens), {Descartados} descartados.",
            hits.Count, hits.Count - inScope.Count, normHits.Count, context.Chunks.Count, context.TokensAproximados, context.Descartados);

        return context;
    }

    private static string BuildQueryText(RagQuery query)
    {
        var text = $"{PecaCatalogo.RotuloTipo(query.TipoPeca)} {PecaCatalogo.RotuloArea(query.Area)}\n{query.Pedidos}\n{query.Fatos}";

        return text.Length <= ConsultaMaxChars ? text : text[..ConsultaMaxChars];
    }

    /// <summary>
    /// O filtro de verdade roda no banco vetorial; isto é a segunda trava. Hit de OUTRA área é
    /// descartado; hit sem área no payload passa (indexação antiga) — a regra de tenant, essa sim,
    /// não admite exceção e já vem do filtro obrigatório do <see cref="IVectorStore"/>.
    /// </summary>
    private static bool IsInScope(VectorHit hit, RagQuery query)
    {
        if (!string.IsNullOrEmpty(hit.Meta.Area)
            && !string.Equals(hit.Meta.Area, query.Area, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return query.DocumentosIds.Count == 0
            || (hit.Meta.DocumentoOrigemId is { } documentoId && query.DocumentosIds.Contains(documentoId));
    }
}

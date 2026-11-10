using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
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
    ILogger<RagService> logger) : IRagService
{
    /// <summary>Quanto da consulta vira texto de embedding: o começo dos fatos já carrega o assunto.</summary>
    private const int ConsultaMaxChars = 2000;

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
        var context = ContextSelector.Select(inScope, limit, settings.MaxTokensContexto, settings.CharsPorToken, settings.ScoreMinimo);

        // Só contagens: o texto dos trechos e da consulta nunca vai para o log.
        logger.LogInformation(
            "RAG: {Recuperados} hits, {Fora} fora do escopo, {Escolhidos} no contexto (~{Tokens} tokens), {Descartados} descartados.",
            hits.Count, hits.Count - inScope.Count, context.Chunks.Count, context.TokensAproximados, context.Descartados);

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

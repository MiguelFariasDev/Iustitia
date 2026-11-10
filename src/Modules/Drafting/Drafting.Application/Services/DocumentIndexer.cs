using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Núcleo da indexação: quebra cada página em trechos, gera os embeddings (BGE-M3) e grava no banco
/// vetorial com o tenant, a visibilidade e os metadados no payload. Reindexar o mesmo documento
/// SUBSTITUI os pontos antigos (ids determinísticos + exclusão prévia), então é seguro repetir.
/// Roda no worker, com a GPU ligada; o tenant e o usuário vêm da solicitação (não há requisição HTTP).
/// </summary>
public sealed partial class DocumentIndexer(
    IEmbeddingService embeddings,
    IVectorStore vectorStore,
    ILogger<DocumentIndexer> logger) : IDocumentIndexer
{
    /// <summary>Tamanho máximo do trecho (a faixa pedida é 800–1.200 caracteres).</summary>
    public const int ChunkChars = 1200;
    public const int MinChunkChars = 800;
    public const int OverlapChars = 150;
    public const int EmbeddingDimensions = 1024;
    private const int EmbeddingBatch = 16;

    /// <summary>Início de artigo, cláusula, parágrafo numerado ou capítulo: bons lugares para NÃO cortar no meio.</summary>
    [GeneratedRegex(@"\n(?=\s*(Art\.?\s*\d|Artigo\s+\d|CL[ÁA]USULA|Cl[áa]usula|CAP[ÍI]TULO|Cap[íi]tulo|SE[ÇC][ÃA]O|Se[çc][ãa]o|§\s*\d|Par[áa]grafo))", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingBoundary();

    [GeneratedRegex(@"(Art\.?\s*\d+[ºo°]?(?:-[A-Z])?|Artigo\s+\d+[ºo°]?|CL[ÁA]USULA\s+[\wÀ-ÿ]+|Cl[áa]usula\s+[\wÀ-ÿ]+|§\s*\d+[ºo°]?)", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    public async Task<Result<IndexDocumentResponse>> IndexAsync(
        IndexDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Paginas.Count == 0)
        {
            return Result.Failure<IndexDocumentResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "paginas"));
        }

        var pieces = new List<(int? Page, int Index, string Text, string? Reference)>();
        foreach (var page in request.Paginas)
        {
            var index = 0;
            foreach (var (text, reference) in Split(InputSanitizer.Clean(page.Texto)))
            {
                pieces.Add((page.Numero, index++, text, reference));
            }
        }

        if (pieces.Count == 0)
        {
            return Result.Failure<IndexDocumentResponse>(ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE));
        }

        await vectorStore.EnsureCollectionAsync(EmbeddingDimensions, cancellationToken);
        await vectorStore.DeleteByDocumentAsync(request.TenantId, request.DocumentoOrigemId, cancellationToken);

        var visibilidade = request.Visibilidade.ToLowerInvariant();
        var points = new List<VectorPoint>(pieces.Count);
        for (var offset = 0; offset < pieces.Count; offset += EmbeddingBatch)
        {
            var batch = pieces.Skip(offset).Take(EmbeddingBatch).ToList();
            var vectors = await embeddings.EmbedAsync(batch.Select(p => p.Text).ToList(), cancellationToken);
            if (vectors.Count != batch.Count)
            {
                throw new LlmServiceException("O serviço de embeddings devolveu um número inesperado de vetores.");
            }

            for (var i = 0; i < batch.Count; i++)
            {
                var (page, index, text, reference) = batch[i];
                var meta = new ChunkMetadata(
                    request.DocumentoOrigemId,
                    request.NomeArquivo,
                    page,
                    request.TipoDocumento,
                    request.Area?.Trim().ToLowerInvariant(),
                    request.Tribunal,
                    request.DataPublicacao,
                    request.UrlOrigem,
                    ContextSelector.HashText(text),
                    request.Tags,
                    request.UsuarioId,
                    visibilidade,
                    reference);

                points.Add(new VectorPoint(
                    request.TenantId, PointId(request.TenantId, request.DocumentoOrigemId, page, index), vectors[i], text, meta));
            }
        }

        await vectorStore.UpsertAsync(points, cancellationToken);
        logger.LogInformation(
            "Peças: documento {DocumentoId} indexado com {Trechos} trechos (tenant {TenantId}).",
            request.DocumentoOrigemId, points.Count, request.TenantId);

        return Result.Success(new IndexDocumentResponse(request.DocumentoOrigemId, points.Count));
    }

    /// <summary>
    /// Quebra em trechos de até <see cref="ChunkChars"/>, com sobreposição, escolhendo o ponto de corte
    /// na ordem: início de artigo/cláusula, fim de parágrafo, fim de frase. Cada trecho leva a última
    /// referência (artigo/cláusula) vista até ali, para preservar o contexto no payload.
    /// </summary>
    public static IEnumerable<(string Text, string? Reference)> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        string? currentReference = null;
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + ChunkChars, text.Length);
            if (end < text.Length)
            {
                end = BestCut(text, start, end);
            }

            var piece = text[start..end].Trim();
            if (piece.Length > 0)
            {
                var found = ReferencePattern().Match(piece);
                if (found.Success)
                {
                    currentReference = found.Value.Trim();
                }

                yield return (piece, currentReference);
            }

            if (end >= text.Length)
            {
                yield break;
            }

            start = Math.Max(end - OverlapChars, start + 1);
        }
    }

    private static int BestCut(string text, int start, int end)
    {
        var segment = text.Substring(start, end - start);

        // 1) Antes de um cabeçalho de artigo/cláusula: o item inteiro fica junto no trecho seguinte.
        var heading = -1;
        foreach (Match match in HeadingBoundary().Matches(segment))
        {
            if (match.Index >= MinChunkChars)
            {
                heading = match.Index;
            }
        }

        if (heading >= 0)
        {
            return start + heading;
        }

        // 2) Fim de parágrafo; 3) fim de frase.
        var cut = segment.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (cut < MinChunkChars)
        {
            cut = segment.LastIndexOf(". ", StringComparison.Ordinal);
        }

        return cut >= MinChunkChars ? start + cut + 1 : end;
    }

    /// <summary>Id determinístico (GUID a partir de SHA-256): reindexar regrava os mesmos pontos.</summary>
    public static string PointId(Guid tenantId, Guid documentoId, int? page, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}|{documentoId:N}|{page?.ToString() ?? "-"}|{index}"));

        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}

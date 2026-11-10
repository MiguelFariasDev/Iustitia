using System.Security.Cryptography;
using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Indexa um documento: quebra cada página em trechos (com sobreposição), gera os embeddings e
/// grava no banco vetorial com o tenant no payload. Reindexar o mesmo documento SUBSTITUI os
/// pontos antigos (ids determinísticos + exclusão prévia), então é seguro repetir.
/// </summary>
public sealed class DocumentIndexer(
    IEmbeddingService embeddings,
    IVectorStore vectorStore,
    ICurrentUser currentUser,
    ILogger<DocumentIndexer> logger) : IDocumentIndexer
{
    public const int ChunkChars = 1200;
    public const int OverlapChars = 150;
    public const int MaxPages = 500;
    public const int MaxTotalChars = 2_000_000;
    public const int EmbeddingDimensions = 1024;
    private const int EmbeddingBatch = 16;

    public async Task<Result<IndexDocumentResponse>> IndexAsync(
        IndexDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<IndexDocumentResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return Result.Failure<IndexDocumentResponse>(validation);
        }

        var nome = InputSanitizer.Clean(request.NomeArquivo);
        var pieces = new List<(int Page, int Index, string Text)>();
        foreach (var page in request.Paginas)
        {
            var index = 0;
            foreach (var text in Split(InputSanitizer.Clean(page.Texto)))
            {
                pieces.Add((page.Numero, index++, text));
            }
        }

        await vectorStore.EnsureCollectionAsync(EmbeddingDimensions, cancellationToken);
        await vectorStore.DeleteByDocumentAsync(tenantId, request.DocumentoOrigemId, cancellationToken);

        var points = new List<VectorPoint>(pieces.Count);
        for (var offset = 0; offset < pieces.Count; offset += EmbeddingBatch)
        {
            var batch = pieces.Skip(offset).Take(EmbeddingBatch).ToList();
            var vectors = await embeddings.EmbedAsync(batch.Select(p => p.Text).ToList(), cancellationToken);
            if (vectors.Count != batch.Count)
            {
                throw new InvalidOperationException("O serviço de embeddings devolveu um número inesperado de vetores.");
            }

            for (var i = 0; i < batch.Count; i++)
            {
                var (page, index, text) = batch[i];
                var meta = new ChunkMetadata(
                    request.DocumentoOrigemId,
                    nome,
                    page,
                    request.TipoDocumento,
                    request.Area?.Trim().ToLowerInvariant(),
                    request.Tribunal,
                    request.DataPublicacao,
                    request.UrlOrigem,
                    ContextSelector.HashText(text));

                points.Add(new VectorPoint(tenantId, PointId(tenantId, request.DocumentoOrigemId, page, index), vectors[i], text, meta));
            }
        }

        await vectorStore.UpsertAsync(points, cancellationToken);
        logger.LogInformation(
            "Peças: documento {DocumentoId} indexado com {Trechos} trechos (tenant {TenantId}).",
            request.DocumentoOrigemId, points.Count, tenantId);

        return Result.Success(new IndexDocumentResponse(request.DocumentoOrigemId, points.Count));
    }

    /// <summary>Quebra em trechos de ~<see cref="ChunkChars"/>, preferindo fim de parágrafo/frase, com sobreposição.</summary>
    public static IEnumerable<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + ChunkChars, text.Length);
            if (end < text.Length)
            {
                var segment = text.Substring(start, end - start);
                var cut = segment.LastIndexOf("\n\n", StringComparison.Ordinal);
                if (cut < ChunkChars / 2)
                {
                    cut = segment.LastIndexOf(". ", StringComparison.Ordinal);
                }

                if (cut >= ChunkChars / 2)
                {
                    end = start + cut + 1;
                }
            }

            var piece = text[start..end].Trim();
            if (piece.Length > 0)
            {
                yield return piece;
            }

            if (end >= text.Length)
            {
                yield break;
            }

            start = Math.Max(end - OverlapChars, start + 1);
        }
    }

    /// <summary>Id determinístico (GUID a partir de SHA-256): reindexar regrava os mesmos pontos.</summary>
    public static string PointId(Guid tenantId, Guid documentoId, int page, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}|{documentoId:N}|{page}|{index}"));

        return new Guid(hash.AsSpan(0, 16)).ToString();
    }

    private static Error? Validate(IndexDocumentRequest request)
    {
        if (request.DocumentoOrigemId == Guid.Empty || string.IsNullOrWhiteSpace(request.NomeArquivo))
        {
            return ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "nomeArquivo");
        }

        if (request.Paginas is null || request.Paginas.Count == 0)
        {
            return ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "paginas");
        }

        if (request.Paginas.Count > MaxPages)
        {
            return ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "paginas", MaxPages);
        }

        if (request.Paginas.Sum(p => (long)(p.Texto?.Length ?? 0)) > MaxTotalChars)
        {
            return ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "paginas", MaxTotalChars);
        }

        if (!string.IsNullOrWhiteSpace(request.UrlOrigem)
            && !(Uri.TryCreate(request.UrlOrigem, UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
        {
            return ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "urlOrigem");
        }

        return null;
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Upload de documentos para o acervo. O upload NÃO usa GPU nem bloqueia a criação de peças:
/// extrai o texto (CPU), valida, deduplica por hash e enfileira a indexação; os embeddings saem
/// quando a GPU estiver ligada por outro motivo — ou quando esta própria fila a ligar.
/// </summary>
public sealed class DocumentIngestionService(
    IOptions<DraftingOptions> draftingOptions,
    IOptions<IngestaoOptions> ingestaoOptions,
    IDocumentTextExtractor extractor,
    IIndexedDocumentRepository repository,
    IGeracaoPecaFila fila,
    IVectorStore vectorStore,
    IDraftingUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IDocumentIngestionService
{
    private const int MaxTags = 10;
    private const int MaxTagLength = 40;

    public async Task<Result<UploadDocumentoResponse>> EnviarAsync(
        UploadDocumentoRequest metadados, Stream conteudo, string nomeArquivo, long tamanhoBytes,
        CancellationToken cancellationToken = default)
    {
        if (!draftingOptions.Value.Enabled)
        {
            return Result.Failure<UploadDocumentoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DISABLED));
        }

        if (currentUser.TenantId is not { } tenantId || currentUser.UserId is not { } userId)
        {
            return Result.Failure<UploadDocumentoResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var options = ingestaoOptions.Value;
        var limite = options.MaxMegabytes * 1024L * 1024L;
        if (tamanhoBytes > limite)
        {
            return Result.Failure<UploadDocumentoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, options.MaxMegabytes));
        }

        var validated = ValidateMetadata(metadados);
        if (validated.IsFailure)
        {
            return Result.Failure<UploadDocumentoResponse>(validated.Error);
        }

        var nome = InputSanitizer.Clean(Path.GetFileName(nomeArquivo));
        if (nome.Length == 0)
        {
            return Result.Failure<UploadDocumentoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "arquivo"));
        }

        // O tamanho declarado pode mentir: o que vale é o que foi lido, até o limite.
        using var buffer = new MemoryStream();
        await conteudo.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > limite)
        {
            return Result.Failure<UploadDocumentoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FILE_TOO_LARGE, options.MaxMegabytes));
        }

        buffer.Position = 0;
        var extracted = extractor.Extract(buffer, nome);
        if (extracted.IsFailure)
        {
            return Result.Failure<UploadDocumentoResponse>(extracted.Error);
        }

        var pages = extracted.Value
            .Select(p => new ExtractedPage(p.Numero, InputSanitizer.Clean(p.Texto)))
            .Where(p => p.Texto.Length > 0)
            .ToList();

        if (pages.Count == 0 || pages.Count > options.MaxPaginas)
        {
            return Result.Failure<UploadDocumentoResponse>(pages.Count == 0
                ? ErrorFactory.From(ErrorCode.DRAFTING_UNSUPPORTED_FILE)
                : ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "paginas", options.MaxPaginas));
        }

        var hash = ContextSelector.HashText(string.Join('\n', pages.Select(p => p.Texto)));
        var duplicate = await repository.FindByHashAsync(hash, cancellationToken);
        if (duplicate is not null)
        {
            return Result.Failure<UploadDocumentoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DOCUMENT_DUPLICATE));
        }

        var m = validated.Value;
        var document = IndexedDocument.Create(
            tenantId, userId, nome, m.Tipo, m.Area, m.Tribunal, m.Data, m.Url,
            m.Tags.Count == 0 ? null : JsonSerializer.Serialize(m.Tags),
            m.Visibilidade, hash, buffer.Length, JsonSerializer.Serialize(pages), clock.UtcNow);

        // Metadados e item da fila na mesma transação.
        repository.Add(document);
        fila.Enfileirar(tenantId, QueueItemKind.Indexacao, document.Id.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UploadDocumentoResponse(
            document.Id.Value,
            document.Status.ToString(),
            "Documento recebido. Ele será indexado assim que o ambiente de IA estiver disponível."));
    }

    public async Task<Result<DocumentoResponse>> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await LoadVisibleAsync(id, cancellationToken);

        return document is null
            ? Result.Failure<DocumentoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND))
            : Result.Success(ToResponse(document));
    }

    public async Task<Result<IReadOnlyList<DocumentoResponse>>> ListarAsync(
        int pagina, int tamanho, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<DocumentoResponse>>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var take = Math.Clamp(tamanho, 1, 100);
        var items = await repository.ListVisibleAsync(userId, Math.Max(0, pagina - 1) * take, take, cancellationToken);

        return Result.Success<IReadOnlyList<DocumentoResponse>>(items.Select(ToResponse).ToList());
    }

    public async Task<Result> RemoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await LoadVisibleAsync(id, cancellationToken);
        if (document is null || !CanRemove(document))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_DOCUMENT_NOT_FOUND));
        }

        try
        {
            await vectorStore.DeleteByDocumentAsync(document.TenantId, document.Id.Value, cancellationToken);
        }
        catch (VectorStoreException)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.DRAFTING_VECTOR_STORE_UNAVAILABLE));
        }

        repository.Remove(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>Documento do escritório, ou privado do próprio usuário. Privado de outro usuário é "não encontrado".</summary>
    private async Task<IndexedDocument?> LoadVisibleAsync(Guid id, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(IndexedDocumentId.From(id), ct);
        if (document is null)
        {
            return null;
        }

        return document.Visibilidade == DocumentVisibility.Escritorio || document.UsuarioId == currentUser.UserId
            ? document
            : null;
    }

    private bool CanRemove(IndexedDocument document) =>
        document.UsuarioId == currentUser.UserId
        || (document.Visibilidade == DocumentVisibility.Escritorio
            && (currentUser.IsInRole("Owner") || currentUser.IsInRole("Partner")));

    private static DocumentoResponse ToResponse(IndexedDocument d) =>
        new(
            d.Id.Value, d.NomeArquivo, d.TipoDocumento, d.Area, d.Tribunal, d.Visibilidade.ToString().ToLowerInvariant(),
            d.Status.ToString(), d.Trechos, d.CriadoEm, d.IndexadoEm);

    private sealed record Metadata(
        string Tipo, string? Area, string? Tribunal, DateOnly? Data, string? Url, IReadOnlyList<string> Tags, DocumentVisibility Visibilidade);

    private static Result<Metadata> ValidateMetadata(UploadDocumentoRequest request)
    {
        var tipo = (request.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();
        if (!PecaCatalogo.TiposDocumento.Contains(tipo))
        {
            return Result.Failure<Metadata>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "tipoDocumento"));
        }

        string? area = null;
        if (!string.IsNullOrWhiteSpace(request.Area))
        {
            area = request.Area.Trim().ToLowerInvariant();
            if (!PecaCatalogo.Areas.ContainsKey(area))
            {
                return Result.Failure<Metadata>(ErrorFactory.From(ErrorCode.DRAFTING_INVALID_AREA));
            }
        }

        var tribunal = InputSanitizer.Clean(request.Tribunal).ToUpperInvariant();
        if (tribunal.Length > 20)
        {
            return Result.Failure<Metadata>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "tribunal", 20));
        }

        string? url = null;
        if (!string.IsNullOrWhiteSpace(request.UrlOrigem))
        {
            if (!Uri.TryCreate(request.UrlOrigem.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return Result.Failure<Metadata>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "urlOrigem"));
            }

            url = uri.ToString();
        }

        var tags = (request.Tags ?? [])
            .Select(t => InputSanitizer.Clean(t).ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Select(t => t.Length > MaxTagLength ? t[..MaxTagLength] : t)
            .Distinct()
            .Take(MaxTags)
            .ToList();

        var visibilidade = DocumentVisibility.Escritorio;
        if (!string.IsNullOrWhiteSpace(request.Visibilidade)
            && !Enum.TryParse(request.Visibilidade.Trim(), ignoreCase: true, out visibilidade))
        {
            return Result.Failure<Metadata>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "visibilidade"));
        }

        return Result.Success(new Metadata(
            tipo, area, tribunal.Length == 0 ? null : tribunal, request.DataPublicacao, url, tags, visibilidade));
    }
}

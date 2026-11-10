using System.Net.Http;
using System.Text.Json;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Indexa um documento enviado ao acervo (embeddings na GPU + gravação no Qdrant). Roda no mesmo
/// worker e na mesma fila das peças: a GPU liga uma vez e atende os dois tipos de trabalho.
/// </summary>
public sealed class IndexacaoWorkHandler(
    IIndexedDocumentRepository repository,
    IDocumentIndexer indexer,
    IDraftingUnitOfWork unitOfWork,
    Advocacia.BuildingBlocks.Domain.Time.IDateTimeProvider clock,
    IOptions<FilaOptions> filaOptions,
    ILogger<IndexacaoWorkHandler> logger) : IQueueWorkHandler
{
    public QueueItemKind Tipo => QueueItemKind.Indexacao;

    // O documento fica Pendente enquanto espera; não há etapas intermediárias a mostrar.
    public Task AoReservarAsync(Guid referenciaId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AoPrepararGpuAsync(Guid referenciaId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AoFaltarInfraAsync(Guid referenciaId, string motivo, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task AoExpirarAsync(Guid referenciaId, CancellationToken cancellationToken = default) =>
        FailAsync(referenciaId, "Tempo máximo de espera na fila esgotado.", cancellationToken);

    public Task AoFalharDefinitivoAsync(Guid referenciaId, string erro, CancellationToken cancellationToken = default) =>
        FailAsync(referenciaId, erro, cancellationToken);

    public async Task<WorkOutcome> ProcessarAsync(Guid referenciaId, int tentativa, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(IndexedDocumentId.From(referenciaId), cancellationToken);
        if (document is null)
        {
            // Removido enquanto esperava: nada a indexar, e não é falha.
            return new WorkOutcome(WorkOutcomeKind.Cancelado);
        }

        if (document.Status == IndexedDocumentStatus.Indexado)
        {
            return new WorkOutcome(WorkOutcomeKind.Concluido);
        }

        var pages = ParsePages(document.PaginasJson);
        if (pages.Count == 0)
        {
            document.MarkFailed("Documento sem texto extraído para indexar.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new WorkOutcome(WorkOutcomeKind.Falhou, "sem texto");
        }

        try
        {
            document.MarkIndexing();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = await indexer.IndexAsync(
                new IndexDocumentRequest(
                    document.TenantId,
                    document.UsuarioId,
                    document.Id.Value,
                    document.NomeArquivo,
                    document.TipoDocumento,
                    document.Area,
                    document.Tribunal,
                    document.DataPublicacao,
                    document.UrlOrigem,
                    ParseTags(document.TagsJson),
                    document.Visibilidade.ToString().ToLowerInvariant(),
                    pages),
                cancellationToken);

            if (result.IsFailure)
            {
                document.MarkFailed(result.Error.Message);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return new WorkOutcome(WorkOutcomeKind.Falhou, result.Error.Code);
            }

            document.MarkIndexed(result.Value.Trechos, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new WorkOutcome(WorkOutcomeKind.Concluido);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new WorkOutcome(WorkOutcomeKind.Interrompido, "Processo interrompido.");
        }
        catch (Exception exception) when (exception is LlmServiceException or VectorStoreException or TimeoutException or HttpRequestException)
        {
            var motivo = $"{exception.GetType().Name}: {exception.Message}";
            logger.LogWarning(exception, "Peças: indexação do documento {DocumentoId} falhou (tentativa {Tentativa}).", referenciaId, tentativa);

            if (tentativa < filaOptions.Value.MaxTentativas)
            {
                document.MarkPending(motivo);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);

                return new WorkOutcome(WorkOutcomeKind.Reagendar, motivo);
            }

            document.MarkFailed(motivo);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            return new WorkOutcome(WorkOutcomeKind.Falhou, motivo);
        }
    }

    private async Task FailAsync(Guid referenciaId, string erro, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(IndexedDocumentId.From(referenciaId), ct);
        if (document is null || document.Status == IndexedDocumentStatus.Indexado)
        {
            return;
        }

        document.MarkFailed(erro);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static List<IndexPage> ParsePages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<ExtractedPage>>(json) ?? [])
                .Select(p => new IndexPage(p.Numero, p.Texto))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

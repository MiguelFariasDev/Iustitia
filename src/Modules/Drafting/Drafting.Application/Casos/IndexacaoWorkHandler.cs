using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Casos;

/// <summary>
/// Classifica o anexo e o indexa na coleção <c>documentos_caso</c> (denso BGE-M3 + esparso BM25). Usa a mesma
/// fila das peças: a GPU liga uma vez e atende tudo. Reprocessar é seguro: ids determinísticos e os pontos
/// antigos saem antes.
/// </summary>
public sealed class IndexacaoWorkHandler(
    ICasoDocumentoRepository documentos,
    IClassificacaoDocumentoService classificacao,
    IEmbeddingService embeddings,
    ICasoVectorStore vetores,
    IQdrantService qdrant,
    IDraftingUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<FilaOptions> filaOptions,
    ILogger<IndexacaoWorkHandler> logger) : IQueueWorkHandler
{
    public QueueItemKind Tipo => QueueItemKind.Indexacao;

    public Task AoReservarAsync(Guid referenciaId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AoPrepararGpuAsync(Guid referenciaId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AoFaltarInfraAsync(Guid referenciaId, string motivo, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AoExpirarAsync(Guid referenciaId, CancellationToken cancellationToken = default) =>
        FalharAsync(referenciaId, "Tempo máximo de espera na fila esgotado.", cancellationToken);

    public Task AoFalharDefinitivoAsync(Guid referenciaId, string erro, CancellationToken cancellationToken = default) =>
        FalharAsync(referenciaId, erro, cancellationToken);

    public async Task<WorkOutcome> ProcessarAsync(Guid referenciaId, int tentativa, CancellationToken cancellationToken = default)
    {
        var documento = await documentos.ObterAsync(CasoDocumentoId.From(referenciaId), cancellationToken);
        if (documento is null)
        {
            // Removido enquanto esperava (ou item de fila de versão antiga): nada a fazer.
            return new WorkOutcome(WorkOutcomeKind.Cancelado);
        }

        if (documento.Status == CasoDocumentoStatus.Indexado)
        {
            return new WorkOutcome(WorkOutcomeKind.Concluido);
        }

        var chunks = await documentos.ListarChunksAsync(documento.Id, cancellationToken);
        if (chunks.Count == 0)
        {
            documento.MarcarFalha("Documento sem trechos para indexar.");
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new WorkOutcome(WorkOutcomeKind.Falhou, "sem trechos");
        }

        try
        {
            documento.MarcarIndexando();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await ClassificarAsync(documento, chunks, cancellationToken);
            await IndexarAsync(documento, chunks, cancellationToken);

            documento.MarcarIndexado(chunks.Count, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Anexos: {DocumentoId} indexado com {Trechos} trechos.", referenciaId, chunks.Count);

            return new WorkOutcome(WorkOutcomeKind.Concluido);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            documento.Reagendar(CasoDocumentoStatus.Convertido, "Processo interrompido.");
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            return new WorkOutcome(WorkOutcomeKind.Interrompido, "Processo interrompido.");
        }
        catch (Exception ex) when (ex is LlmServiceException or VectorStoreException or TimeoutException or HttpRequestException)
        {
            var motivo = $"{ex.GetType().Name}: {ex.Message}";
            logger.LogWarning(ex, "Anexos: indexação de {DocumentoId} falhou (tentativa {Tentativa}).", referenciaId, tentativa);
            if (tentativa < filaOptions.Value.MaxTentativas)
            {
                documento.Reagendar(CasoDocumentoStatus.Convertido, motivo);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);

                return new WorkOutcome(WorkOutcomeKind.Reagendar, motivo);
            }

            documento.MarcarFalha(motivo);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            return new WorkOutcome(WorkOutcomeKind.Falhou, motivo);
        }
    }

    private async Task ClassificarAsync(CasoDocumento documento, IReadOnlyList<CasoDocumentoChunk> chunks, CancellationToken ct)
    {
        // Trecho suspeito não é mostrado a modelo nenhum, nem ao classificador.
        var texto = string.Join("\n\n", chunks.Where(c => !c.Suspeito).OrderBy(c => c.Ordem).Select(c => c.Texto));
        if (texto.Length == 0)
        {
            return;
        }

        var resultado = await classificacao.ClassificarAsync(documento.NomeArquivo, texto, documento.Origem, documento.ConfiancaOcr, ct);
        documento.AplicarClassificacao(JsonSerializer.Serialize(resultado), resultado.TipoDocumento, resultado.Area);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task IndexarAsync(CasoDocumento documento, IReadOnlyList<CasoDocumentoChunk> chunks, CancellationToken ct)
    {
        await qdrant.GarantirColecoesAsync(ct);
        await vetores.RemoverDocumentoAsync(documento.TenantId, documento.CasoId.Value, documento.Id.Value, ct);

        for (var inicio = 0; inicio < chunks.Count; inicio += EmbeddingConstants.Lote)
        {
            var lote = chunks.Skip(inicio).Take(EmbeddingConstants.Lote).ToList();
            var densos = await embeddings.EmbedAsync(lote.Select(c => ComContexto(documento, c)).ToList(), ct);
            if (densos.Count != lote.Count || densos.Any(v => v.Length != EmbeddingConstants.Dimensoes))
            {
                throw new LlmServiceException("O serviço de embeddings devolveu vetores em formato inesperado.");
            }

            await vetores.UpsertAsync(lote.Select((c, i) => Ponto(documento, c, densos[i])).ToList(), ct);
        }
    }

    /// <summary>Nome e seção entram no texto EMBUTIDO (não no guardado): "cláusula 5 do contrato" precisa ser encontrável.</summary>
    private static string ComContexto(CasoDocumento documento, CasoDocumentoChunk chunk) =>
        $"{documento.NomeArquivo}{(chunk.Secao is null ? string.Empty : $" — {chunk.Secao}")}\n{chunk.Texto}";

    internal static CasoVectorPoint Ponto(CasoDocumento documento, CasoDocumentoChunk chunk, float[] denso) =>
        new(
            chunk.Id,
            denso,
            Bm25.Documento(ComContexto(documento, chunk)),
            chunk.Texto,
            new ChunkMetadata(
                documento.Id.Value, documento.NomeArquivo, chunk.Pagina, documento.TipoDocumento, documento.Area, null,
                documento.DataDocumento, null, chunk.HashSha256,
                Caso: new CasoRef(
                    documento.TenantId, documento.CasoId.Value, documento.ClienteId, documento.Id.Value,
                    documento.UsuarioIdResponsavel, chunk.Origem, chunk.Confianca, chunk.Secao, documento.Restrito, chunk.Suspeito)),
            documento.CriadoEm);

    private async Task FalharAsync(Guid referenciaId, string erro, CancellationToken ct)
    {
        var documento = await documentos.ObterAsync(CasoDocumentoId.From(referenciaId), ct);
        if (documento is null || documento.Status == CasoDocumentoStatus.Indexado)
        {
            return;
        }

        documento.MarcarFalha(erro);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

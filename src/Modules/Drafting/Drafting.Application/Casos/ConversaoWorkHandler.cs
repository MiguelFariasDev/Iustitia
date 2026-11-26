using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Conversao;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Casos;

/// <summary>
/// Lê um anexo: original cifrado → texto/OCR/transcrição → Markdown normalizado → metadados → trechos. Só CPU:
/// a GPU não é ligada por causa disto. Ao terminar, enfileira a classificação e a indexação.
/// </summary>
public sealed class ConversaoWorkHandler(
    ICasoDocumentoRepository documentos,
    ICasoRepository casos,
    ICasoArquivoStore arquivos,
    IConversorDocumentoService conversor,
    IExtracaoMetadadosService metadados,
    IChunkerJuridicoService chunker,
    ISegurancaIaService seguranca,
    IGeracaoPecaFila fila,
    IDraftingUnitOfWork unitOfWork,
    IOptions<FilaOptions> filaOptions,
    ILogger<ConversaoWorkHandler> logger) : IQueueWorkHandler
{
    public QueueItemKind Tipo => QueueItemKind.Conversao;

    public bool RequerGpu => false;

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
            // Removido enquanto esperava: não é falha.
            return new WorkOutcome(WorkOutcomeKind.Cancelado);
        }

        if (documento.Status is CasoDocumentoStatus.Convertido or CasoDocumentoStatus.Indexando or CasoDocumentoStatus.Indexado)
        {
            return new WorkOutcome(WorkOutcomeKind.Concluido);
        }

        try
        {
            return await ConverterAsync(documento, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new WorkOutcome(WorkOutcomeKind.Interrompido, "Processo interrompido.");
        }
        catch (Exception ex) when (ex is StorageException or TimeoutException or HttpRequestException or IOException)
        {
            var motivo = $"{ex.GetType().Name}: {ex.Message}";
            logger.LogWarning(ex, "Anexos: conversão de {DocumentoId} falhou (tentativa {Tentativa}).", referenciaId, tentativa);
            if (tentativa < filaOptions.Value.MaxTentativas)
            {
                documento.Reagendar(CasoDocumentoStatus.Recebido, motivo);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);

                return new WorkOutcome(WorkOutcomeKind.Reagendar, motivo);
            }

            documento.MarcarFalha(motivo);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            return new WorkOutcome(WorkOutcomeKind.Falhou, motivo);
        }
    }

    private async Task<WorkOutcome> ConverterAsync(CasoDocumento documento, CancellationToken ct)
    {
        documento.MarcarConvertendo();
        await unitOfWork.SaveChangesAsync(ct);

        var caso = await casos.ObterAsync(documento.CasoId, ct)
            ?? throw new InvalidOperationException("Anexo sem caso.");
        var original = await arquivos.LerAsync(documento.ChaveOriginal, ct);
        if (original is null)
        {
            return await FalhaDefinitivaAsync(documento, "Arquivo original não encontrado no storage.", ct);
        }

        // Integridade: o que está guardado é o que foi enviado?
        if (TextoHash.DosBytes(original) != documento.HashSha256)
        {
            return await FalhaDefinitivaAsync(documento, "O hash do arquivo guardado não confere com o do envio.", ct);
        }

        var convertido = await conversor.ConverterAsync(
            new DocumentoParaConverter(original, documento.NomeArquivo, documento.Formato), ct);
        if (convertido.IsFailure)
        {
            return await FalhaDefinitivaAsync(documento, $"{convertido.Error.Code}: {convertido.Error.Message}", ct);
        }

        var trechos = chunker.DividirDocumento(convertido.Value);
        if (trechos.Count == 0)
        {
            return await FalhaDefinitivaAsync(documento, "Documento sem texto aproveitável.", ct);
        }

        var texto = string.Join("\n\n", convertido.Value.Blocos.Select(b => b.Markdown));
        var extraidos = metadados.Extrair(texto);
        var suspeitos = trechos.Where(t => t.Suspeito).ToList();

        var chave = $"casos/{documento.TenantId:N}/{documento.CasoId.Value:N}/{documento.Id.Value:N}/documento.md";
        documento.MarcarConvertido(
            chave, convertido.Value.Origem, convertido.Value.Confianca, convertido.Value.Paginas, trechos.Count, suspeitos.Count > 0,
            extraidos.DataDocumento);
        await arquivos.GravarAsync(
            chave, System.Text.Encoding.UTF8.GetBytes(MarkdownDocumento.Render(convertido.Value, documento, caso)), ct);

        await documentos.SubstituirChunksAsync(
            documento.Id,
            trechos.Select(t => new CasoDocumentoChunk(
                documento, t.Ordem, t.Pagina, t.Secao, t.Texto, t.Hash, t.Tokens, t.Origem, t.Confianca, t.Suspeito)).ToList(),
            ct);
        fila.Enfileirar(documento.TenantId, QueueItemKind.Indexacao, documento.Id.Value);
        await unitOfWork.SaveChangesAsync(ct);

        if (suspeitos.Count > 0)
        {
            await seguranca.RegistrarAsync(
                documento.TenantId, documento.UsuarioIdResponsavel, "InjecaoEmAnexo", documento.Id.ToString(), suspeitos[0].Texto, ct);
        }

        logger.LogInformation(
            "Anexos: {DocumentoId} convertido ({Formato}, {Origem}, {Trechos} trechos).",
            documento.Id.Value, documento.Formato, documento.Origem, trechos.Count);

        return new WorkOutcome(WorkOutcomeKind.Concluido);
    }

    private async Task<WorkOutcome> FalhaDefinitivaAsync(CasoDocumento documento, string erro, CancellationToken ct)
    {
        documento.MarcarFalha(erro);
        await unitOfWork.SaveChangesAsync(ct);

        return new WorkOutcome(WorkOutcomeKind.Falhou, erro);
    }

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

using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Persistência e versionamento das peças. Regerar cria OUTRA solicitação (versão + 1) ligada à
/// anterior; nada é sobrescrito. Tenant e usuário vêm do JWT quando há requisição; no worker a
/// solicitação já existe e os dados vêm dela.
/// </summary>
public sealed class LegalDocumentService(
    ILegalDocumentRepository repository,
    IGeracaoPecaFila fila,
    IDraftingUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<CustoOptions> custo) : ILegalDocumentService
{
    public async Task<Result<LegalDocumentRequest>> CreateAndEnqueueAsync(
        PecaInput input, CancellationToken cancellationToken = default)
    {
        if (currentUser.TenantId is not { } tenantId || currentUser.UserId is not { } userId)
        {
            return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var versao = 1;
        LegalDocumentRequestId? origemId = null;

        if (input.PecaOrigemId is { } origem)
        {
            // O filtro por tenant do contexto (e o RLS) fazem uma solicitação de outro escritório
            // aparecer como "não encontrada" — nunca como "proibida".
            var anterior = await repository.GetByIdAsync(LegalDocumentRequestId.From(origem), cancellationToken);
            if (anterior is null)
            {
                return Result.Failure<LegalDocumentRequest>(ErrorFactory.From(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
            }

            versao = anterior.Versao + 1;
            origemId = anterior.Id;
        }

        var documentosJson = input.DocumentosIds.Count == 0 ? null : JsonSerializer.Serialize(input.DocumentosIds);
        var created = LegalDocumentRequest.Create(
            tenantId, userId, input.TipoPeca, input.Area, input.Fatos, input.Pedidos, input.InstrucoesAdicionais,
            documentosJson, clock.UtcNow, versao, origemId);

        if (created.IsFailure)
        {
            return created;
        }

        // Solicitação e item da fila no MESMO SaveChanges: nunca existe pedido sem fila nem fila sem pedido.
        repository.Add(created.Value);
        fila.Enfileirar(tenantId, QueueItemKind.Geracao, created.Value.Id.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task ClaimAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default)
    {
        request.Claim();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStageAsync(
        LegalDocumentRequest request, LegalDocumentStatus status, string etapa, int progresso,
        CancellationToken cancellationToken = default)
    {
        request.SetStage(status, etapa, progresso);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task WaitForInfrastructureAsync(
        LegalDocumentRequest request, string etapa, CancellationToken cancellationToken = default)
    {
        request.WaitForInfrastructure(etapa);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task StartGenerationAsync(
        LegalDocumentRequest request, string promptVersao, string modelo, CancellationToken cancellationToken = default)
    {
        request.StartGeneration(promptVersao, modelo, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SavePartialAsync(
        LegalDocumentRequest request, string textoParcial, int progresso, CancellationToken cancellationToken = default)
    {
        request.SetPartial(textoParcial, progresso);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        LegalDocumentRequest request,
        string texto,
        long tempoMs,
        int? tokensEntrada,
        int? tokensSaida,
        IReadOnlyList<(RetrievedChunk Chunk, bool Utilizada)> fontes,
        IReadOnlyList<string> avisos,
        CancellationToken cancellationToken = default)
    {
        AddSources(request, fontes);
        request.AddUsage(tokensEntrada, tokensSaida);

        // Custo ESTIMADO da geração: tempo de modelo × preço/hora configurado. Não rateia subida/ociosidade da GPU.
        var custoUsd = Math.Round((decimal)tempoMs / 3_600_000m * custo.Value.GpuUsdPorHora, 6);
        var avisosJson = avisos.Count == 0 ? null : JsonSerializer.Serialize(avisos);
        request.Complete(texto, tempoMs, custoUsd, avisosJson, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        LegalDocumentRequest request,
        string erro,
        string? textoParcial,
        long tempoMs,
        IReadOnlyList<(RetrievedChunk Chunk, bool Utilizada)> fontes,
        CancellationToken cancellationToken = default)
    {
        AddSources(request, fontes);
        request.Fail(erro, textoParcial, tempoMs, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RequeueAsync(LegalDocumentRequest request, string erro, CancellationToken cancellationToken = default)
    {
        request.Requeue(erro);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result> CancelAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var result = request.Cancel(clock.UtcNow);
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private void AddSources(LegalDocumentRequest request, IReadOnlyList<(RetrievedChunk Chunk, bool Utilizada)> fontes)
    {
        // Uma nova tentativa não pode duplicar as fontes de uma tentativa anterior que falhou.
        if (request.Sources.Count > 0)
        {
            return;
        }

        foreach (var (chunk, utilizada) in fontes)
        {
            var meta = chunk.Meta;
            var metadados = JsonSerializer.Serialize(new
            {
                meta.TipoDocumento,
                meta.Area,
                meta.Tribunal,
                DataPublicacao = meta.DataPublicacao?.ToString("yyyy-MM-dd"),
                meta.UrlOrigem,
                meta.HashConteudo,
                meta.Referencia,
            });

            repository.AddChild(request.AddSource(
                chunk.PointId, meta.DocumentoOrigemId, meta.NomeArquivo, meta.Pagina, chunk.Texto, chunk.Score, metadados, utilizada));
        }
    }
}

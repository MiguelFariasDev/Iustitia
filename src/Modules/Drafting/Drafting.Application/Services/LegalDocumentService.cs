using System.Text.Json;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Persistência e versionamento das peças. Regerar cria OUTRA solicitação (versão + 1) ligada à
/// anterior; nada é sobrescrito. Tenant e usuário vêm do JWT.
/// </summary>
public sealed class LegalDocumentService(
    ILegalDocumentRepository repository,
    IDraftingUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ILegalDocumentService
{
    public async Task<Result<LegalDocumentRequest>> CreateAsync(PecaInput input, CancellationToken cancellationToken = default)
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

        repository.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task StartAsync(
        LegalDocumentRequest request, string promptVersao, string modelo, CancellationToken cancellationToken = default)
    {
        request.StartProcessing(promptVersao, modelo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        LegalDocumentRequest request,
        string texto,
        long tempoMs,
        IReadOnlyList<(RetrievedChunk Chunk, bool Citada)> fontes,
        CancellationToken cancellationToken = default)
    {
        AddSources(request, fontes);
        request.Complete(texto, tempoMs, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        LegalDocumentRequest request,
        string erro,
        string? textoParcial,
        long tempoMs,
        IReadOnlyList<(RetrievedChunk Chunk, bool Citada)> fontes,
        CancellationToken cancellationToken = default)
    {
        AddSources(request, fontes);
        request.Fail(erro, textoParcial, tempoMs, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void AddSources(LegalDocumentRequest request, IReadOnlyList<(RetrievedChunk Chunk, bool Citada)> fontes)
    {
        foreach (var (chunk, citada) in fontes)
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
            });

            repository.AddChild(request.AddSource(
                chunk.PointId, meta.DocumentoOrigemId, meta.NomeArquivo, meta.Pagina, chunk.Texto, chunk.Score, metadados, citada));
        }
    }
}

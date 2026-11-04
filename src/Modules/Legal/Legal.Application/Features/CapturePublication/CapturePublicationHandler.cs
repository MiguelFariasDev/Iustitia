using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Application.Features.CapturePublication;

/// <summary>
/// Persiste uma publicação, deduplicando por (TenantId, ExternalId, Source) — ver ADR-056.
/// Idempotente: capturar de novo algo já capturado devolve sucesso com
/// <c>Existing = true</c>, porque uma reexecução do job sobre um período que se sobrepõe ao
/// anterior é operação normal, não falha.
///
/// A checagem prévia em código é a primeira camada de deduplicação; a definitiva é o índice
/// único (tenant_id, external_id, source), que é o que segura duas execuções concorrentes
/// do job. A violação do índice sobe como exception de infraestrutura e o Hangfire retenta
/// — na retentativa o registro já existe e o caminho de duplicata resolve.
/// </summary>
public sealed class CapturePublicationHandler(
    IPublicationRepository publicationRepository,
    ILegalUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<CapturePublicationHandler> logger)
    : ICommandHandler<CapturePublicationCommand, CapturePublicationResponse>
{
    public async Task<Result<CapturePublicationResponse>> Handle(
        CapturePublicationCommand request, CancellationToken cancellationToken)
    {
        var existing = await publicationRepository.GetByExternalIdAsync(
            request.TenantId, request.ExternalId, request.Source, cancellationToken);

        if (existing is not null)
        {
            return Result.Success(new CapturePublicationResponse(existing.Id.Value, Existing: true));
        }

        var cnjNumberResult = CNJNumber.Create(request.CnjNumber);
        if (cnjNumberResult.IsFailure)
        {
            return Result.Failure<CapturePublicationResponse>(cnjNumberResult.Error);
        }

        var rawContentResult = RawContent.Create(request.RawContent);
        if (rawContentResult.IsFailure)
        {
            return Result.Failure<CapturePublicationResponse>(rawContentResult.Error);
        }

        var now = dateTimeProvider.UtcNow;

        var publishedAtResult = PublishedAt.Create(request.PublishedAt, now);
        if (publishedAtResult.IsFailure)
        {
            return Result.Failure<CapturePublicationResponse>(publishedAtResult.Error);
        }

        var publicationResult = Publication.Create(
            request.TenantId,
            cnjNumberResult.Value,
            request.ExternalId,
            request.Source,
            rawContentResult.Value,
            publishedAtResult.Value,
            capturedAt: now,
            type: request.Type,
            metadataJson: request.MetadataJson);

        if (publicationResult.IsFailure)
        {
            return Result.Failure<CapturePublicationResponse>(publicationResult.Error);
        }

        var publication = publicationResult.Value;

        // Vincula ao processo já na captura quando a consulta partiu de um processo
        // cadastrado. Deixar para depois exigiria um passo de reconciliação por número CNJ,
        // e uma publicação sem processo não aparece na timeline de ninguém.
        if (request.ProcessId is { } processId && processId != Guid.Empty)
        {
            var attachResult = publication.LinkTo(processId, request.ClienteId, request.ResponsavelUserId);
            if (attachResult.IsFailure)
            {
                return Result.Failure<CapturePublicationResponse>(attachResult.Error);
            }
        }

        publicationRepository.Add(publication);

        // Um SaveChanges: a publicação e a mensagem de outbox do PublicationCapturedEvent
        // entram na mesma transação (OutboxInterceptor — ver ADR-008).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Log sem o RawContent (ver claude.md, seção 14).
        logger.LogInformation(
            "Publicação capturada: {PublicationId} (processo {CnjNumber}, vinculada={Vinculada}, origem {Source}, externalId {ExternalId}).",
            publication.Id.Value, publication.CnjNumber.Value, publication.ProcessId is not null,
            publication.Source, publication.ExternalId);

        return Result.Success(new CapturePublicationResponse(publication.Id.Value, Existing: false));
    }
}

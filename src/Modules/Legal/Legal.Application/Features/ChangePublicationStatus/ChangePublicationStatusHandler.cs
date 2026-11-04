using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Application.Features.ChangePublicationStatus;

/// <summary>
/// As transições permitidas vivem no domínio (<see cref="Publication.ChangeStatus"/>); este
/// handler só carrega, delega e persiste. A auditoria do "quem decidiu" é carimbada pelo
/// AuditInterceptor (UpdatedBy) e o evento PublicationStatusChangedEvent vai para a outbox.
/// </summary>
public sealed class ChangePublicationStatusHandler(
    IPublicationRepository publicationRepository, ILegalUnitOfWork unitOfWork)
    : ICommandHandler<ChangePublicationStatusCommand>
{
    public async Task<Result> Handle(ChangePublicationStatusCommand request, CancellationToken cancellationToken)
    {
        var publication = await publicationRepository.GetByIdAsync(
            PublicationId.From(request.PublicationId), cancellationToken);

        if (publication is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PUBLICATION_NOT_FOUND));
        }

        // O validator já garantiu o parse; TryParse aqui só evita lançar se alguém despachar sem pipeline.
        if (!Enum.TryParse<PublicationStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Status)));
        }

        var result = publication.ChangeStatus(status, request.Notes);
        if (result.IsFailure)
        {
            return result;
        }

        publicationRepository.Update(publication);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

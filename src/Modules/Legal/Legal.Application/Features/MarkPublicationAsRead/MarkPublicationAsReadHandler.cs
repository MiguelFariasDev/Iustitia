using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Application.Features.MarkPublicationAsRead;

/// <summary>
/// Marca a publicação como lida. "Lida" é só o controle de leitura do usuário — NÃO é
/// revisão nem aprovação: o status de revisão muda por <c>ChangeStatus</c>, numa ação
/// separada e explícita, para que ler uma publicação nunca possa ser confundido com aprovar
/// o que ela implica (ver ADR-012).
/// </summary>
public sealed class MarkPublicationAsReadHandler(
    IPublicationRepository publicationRepository,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<MarkPublicationAsReadCommand>
{
    public async Task<Result> Handle(MarkPublicationAsReadCommand request, CancellationToken cancellationToken)
    {
        var publication = await publicationRepository.GetByIdAsync(
            PublicationId.From(request.PublicationId), cancellationToken);

        if (publication is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.PUBLICATION_NOT_FOUND));
        }

        var result = publication.MarkAsRead();
        if (result.IsFailure)
        {
            return result;
        }

        publicationRepository.Update(publication);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

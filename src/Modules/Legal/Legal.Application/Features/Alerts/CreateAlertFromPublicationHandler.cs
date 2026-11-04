using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Application.Features.Alerts;

/// <summary>
/// Monta e publica o alerta de uma publicação. Só usa a porta <see cref="IAlertPublisher"/>
/// (BuildingBlocks) — Legal não conhece Platform (ADR-055/ADR-065). A criação é idempotente
/// por (usuário, tipo, publicação), então reexecutar a captura não repete alertas.
///
/// O texto do alerta leva só número do processo, tipo e tribunal: o inteiro teor da publicação
/// nunca é copiado para o alerta, que trafega pelo Realtime (ver claude.md, seção 14).
/// </summary>
public sealed class CreateAlertFromPublicationHandler(
    IPublicationRepository publicationRepository, IAlertPublisher alertPublisher)
    : ICommandHandler<CreateAlertFromPublicationCommand, CreateAlertFromPublicationResponse>
{
    public async Task<Result<CreateAlertFromPublicationResponse>> Handle(
        CreateAlertFromPublicationCommand request, CancellationToken cancellationToken)
    {
        var publication = await publicationRepository.GetByIdAsync(
            PublicationId.From(request.PublicationId), cancellationToken);

        if (publication is null)
        {
            return Result.Failure<CreateAlertFromPublicationResponse>(ErrorFactory.From(ErrorCode.PUBLICATION_NOT_FOUND));
        }

        var created = await alertPublisher.PublishAsync(
            new AlertRequest(
                publication.TenantId,
                request.UserId,
                "Publication",
                BuildTitle(publication),
                BuildDescription(publication),
                publication.Id.Value),
            cancellationToken);

        return Result.Success(new CreateAlertFromPublicationResponse(created));
    }

    private static string BuildTitle(Publication publication) =>
        publication.Type is { } type ? $"Nova publicação: {Label(type)}" : "Nova publicação";

    private static string BuildDescription(Publication publication) =>
        publication.Tribunal is { } tribunal
            ? $"Processo {publication.CnjNumber.Value} — {tribunal}"
            : $"Processo {publication.CnjNumber.Value}";

    private static string Label(PublicationType type) => type switch
    {
        PublicationType.Decisao => "decisão",
        PublicationType.Despacho => "despacho",
        PublicationType.Intimacao => "intimação",
        PublicationType.Sentenca => "sentença",
        PublicationType.Acordao => "acórdão",
        _ => "publicação",
    };
}

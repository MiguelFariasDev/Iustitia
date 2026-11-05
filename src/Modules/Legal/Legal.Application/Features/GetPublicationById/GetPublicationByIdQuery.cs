using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.GetPublicationById;

[Audited("Viewed", "Publication", EntityIdProperty = nameof(PublicationId))]
public sealed record GetPublicationByIdQuery(Guid PublicationId) : IQuery<GetPublicationByIdResponse>;

using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.GetPublicationById;

public sealed record GetPublicationByIdQuery(Guid PublicationId) : IQuery<GetPublicationByIdResponse>;

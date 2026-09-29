using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<GetUserByIdResponse>;

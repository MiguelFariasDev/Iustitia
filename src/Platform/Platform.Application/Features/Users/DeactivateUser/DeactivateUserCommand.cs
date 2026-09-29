using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId) : ICommand<DeactivateUserResponse>;

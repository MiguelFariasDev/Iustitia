using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.UpdateUserRole;

public sealed record UpdateUserRoleCommand(Guid UserId, string Role) : ICommand<UpdateUserRoleResponse>;

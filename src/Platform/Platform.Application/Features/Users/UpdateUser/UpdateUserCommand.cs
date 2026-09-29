using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string Name,
    string? Specialty,
    string? AvatarUrl,
    string? Phone) : ICommand<UpdateUserResponse>;

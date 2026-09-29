using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.InviteUser;

public sealed record InviteUserCommand(string Email, string Name, string Role, string? Specialty) : ICommand<InviteUserResponse>;

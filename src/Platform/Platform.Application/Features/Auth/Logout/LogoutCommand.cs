using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string AccessToken) : ICommand<LogoutResponse>;

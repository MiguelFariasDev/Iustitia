using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Auth.Login;
using Advocacia.Platform.Application.Features.Auth.Logout;
using Advocacia.Platform.Application.Features.Auth.RefreshToken;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;

namespace Advocacia.Platform.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .AllowAnonymous();

        group.MapPost("/logout", async (LogoutCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        return app;
    }
}

using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Auth.Login;
using Advocacia.Platform.Application.Features.Auth.Logout;
using Advocacia.Platform.Application.Features.Auth.RefreshToken;
using Advocacia.Platform.Application.Features.Auth.RegisterSession;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;

namespace Advocacia.Platform.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapVersionedGroup("/auth", "Auth");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .AllowAnonymous();

        group.MapPost("/session", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new RegisterSessionCommand(), cancellationToken)).ToHttpResult())
            .WithName("RegisterSession")
            .WithSummary("Registra o login de quem entrou direto no Supabase Auth (último acesso + auditoria).")
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapPost("/logout", async (LogoutCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        return app;
    }
}

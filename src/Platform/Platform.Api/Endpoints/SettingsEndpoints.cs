using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Settings.GetSettings;
using Advocacia.Platform.Application.Features.Settings.UpdateSettings;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;

namespace Advocacia.Platform.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings").WithTags("Settings");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetSettingsQuery(), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapPut("/", async (UpdateSettingsCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }
}

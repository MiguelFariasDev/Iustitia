using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.FeatureFlags.GetFeatureFlags;
using Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;

namespace Advocacia.Platform.Api.Endpoints;

public static class FeatureFlagsEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/feature-flags").WithTags("FeatureFlags");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetFeatureFlagsQuery(), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapPut("/{key}", async (string key, UpdateFeatureFlagRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new UpdateFeatureFlagCommand(key, request.IsEnabled), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }

    private sealed record UpdateFeatureFlagRequest(bool IsEnabled);
}

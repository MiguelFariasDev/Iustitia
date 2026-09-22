using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Application.Features.Tenants.GetTenantById;
using Advocacia.Platform.Application.Features.Tenants.ReactivateTenant;
using Advocacia.Platform.Application.Features.Tenants.SuspendTenant;
using Advocacia.Platform.Application.Features.Tenants.UpdateTenant;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;

namespace Advocacia.Platform.Api.Endpoints;

public static class TenantsEndpoints
{
    public static IEndpointRouteBuilder MapTenantsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants").WithTags("Tenants");

        // Cadastro self-service de escritório — não exige autenticação prévia (é assim que
        // um novo cliente se cadastra) — ver CreateTenantHandler.
        group.MapPost("/", async (CreateTenantCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken))
                    .ToHttpResult(value => Results.Created($"/api/v1/tenants/{value.TenantId}", value)))
            .AllowAnonymous();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetTenantByIdQuery(id), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly);

        group.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new UpdateTenantCommand(id, request.Name, request.LogoUrl, request.AddressJson), cancellationToken))
                    .ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly);

        group.MapPost("/{id:guid}/suspend", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new SuspendTenantCommand(id), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ReactivateTenantCommand(id), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.OwnerOnly);

        return app;
    }

    private sealed record UpdateTenantRequest(string Name, string? LogoUrl, string? AddressJson);
}

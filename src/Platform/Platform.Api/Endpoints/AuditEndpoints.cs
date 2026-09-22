using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Audit.GetAuditLog;
using Advocacia.Platform.Application.Features.Audit.ListAuditLogs;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Advocacia.Platform.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit").WithTags("Audit");

        group.MapGet("/", async ([AsParameters] ListAuditLogsRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListAuditLogsQuery(request.Page ?? 1, request.PageSize ?? 20, request.EntityType, request.UserId),
                    cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetAuditLogQuery(id), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }

    private sealed record ListAuditLogsRequest(int? Page, int? PageSize, string? EntityType, Guid? UserId);
}

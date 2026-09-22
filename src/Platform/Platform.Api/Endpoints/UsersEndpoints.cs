using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Application.Features.Users.AcceptInvite;
using Advocacia.Platform.Application.Features.Users.DeactivateUser;
using Advocacia.Platform.Application.Features.Users.GetUserById;
using Advocacia.Platform.Application.Features.Users.InviteUser;
using Advocacia.Platform.Application.Features.Users.ListUsers;
using Advocacia.Platform.Application.Features.Users.UpdateUser;
using Advocacia.Platform.Application.Features.Users.UpdateUserRole;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Advocacia.Platform.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        group.MapPost("/invite", async (InviteUserCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapPost("/accept-invite", async (AcceptInviteCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(command, cancellationToken)).ToHttpResult())
            .AllowAnonymous();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetUserByIdQuery(id), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapGet("/", async ([AsParameters] ListUsersRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(
                    new ListUsersQuery(request.Page ?? 1, request.PageSize ?? 20, request.Role, request.Specialty, request.IsActive),
                    cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new UpdateUserCommand(id, request.Name, request.Specialty, request.AvatarUrl, request.Phone), cancellationToken))
                    .ToHttpResult())
            .RequireAuthorization(Policies.AnyAuthenticatedUser);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new DeactivateUserCommand(id), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        group.MapPut("/{id:guid}/role", async (Guid id, UpdateUserRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new UpdateUserRoleCommand(id, request.Role), cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.PartnerOrAbove);

        return app;
    }

    private sealed record ListUsersRequest(int? Page, int? PageSize, string? Role, string? Specialty, bool? IsActive);

    private sealed record UpdateUserRequest(string Name, string? Specialty, string? AvatarUrl, string? Phone);

    private sealed record UpdateUserRoleRequest(string Role);
}

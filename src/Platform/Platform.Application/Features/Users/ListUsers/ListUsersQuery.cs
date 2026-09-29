using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Platform.Application.Features.Users.ListUsers;

public sealed record ListUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Role = null,
    string? Specialty = null,
    bool? IsActive = null) : IQuery<ListUsersResponse>;

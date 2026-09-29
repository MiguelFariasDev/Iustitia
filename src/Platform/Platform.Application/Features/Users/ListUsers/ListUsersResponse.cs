namespace Advocacia.Platform.Application.Features.Users.ListUsers;

public sealed record UserListItem(Guid Id, string Name, string Email, string? Specialty, string Role, bool IsActive);

public sealed record ListUsersResponse(
    IReadOnlyList<UserListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

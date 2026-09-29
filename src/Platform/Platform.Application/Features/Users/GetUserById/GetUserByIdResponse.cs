namespace Advocacia.Platform.Application.Features.Users.GetUserById;

public sealed record GetUserByIdResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    string? Specialty,
    string Role,
    string? AvatarUrl,
    string? Phone,
    bool IsActive,
    DateTimeOffset? LastLoginAt);

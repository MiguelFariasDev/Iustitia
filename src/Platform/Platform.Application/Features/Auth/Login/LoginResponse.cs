namespace Advocacia.Platform.Application.Features.Auth.Login;

public sealed record LoginResponse(
    Guid UserId,
    string Email,
    string Name,
    Guid TenantId,
    string Role,
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);

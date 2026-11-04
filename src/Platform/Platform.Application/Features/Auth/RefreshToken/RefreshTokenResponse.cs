namespace Advocacia.Platform.Application.Features.Auth.RefreshToken;

public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);

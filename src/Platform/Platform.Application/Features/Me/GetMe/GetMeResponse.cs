namespace Advocacia.Platform.Application.Features.Me.GetMe;

public sealed record GetMeResponse(Guid? UserId, string? Email, Guid? TenantId, IReadOnlyCollection<string> Roles);

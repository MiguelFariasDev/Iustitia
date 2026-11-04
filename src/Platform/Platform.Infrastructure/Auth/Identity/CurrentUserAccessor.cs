using System.Security.Claims;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Advocacia.Platform.Infrastructure.Auth.Identity;

/// <summary>
/// Implementação real de <see cref="ICurrentUser"/>, lendo as claims do JWT já validado
/// pelo JwtBearer (ver JwtValidationExtensions) a partir do HttpContext da requisição
/// atual. "tenant_id" e "user_role" são claims customizadas injetadas pelo Custom Access
/// Token Hook do Supabase (ver infra/supabase/migrations/004_custom_access_token_hook.sql)
/// — "role" sozinho é reservado pelo próprio Supabase (sempre "authenticated").
/// </summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = Principal?.FindFirstValue("tenant_id");
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }

    public string? Email => Principal?.FindFirstValue("email");

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll("user_role").Select(claim => claim.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}

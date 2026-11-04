using Advocacia.Platform.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.Infrastructure.Auth.Authorization;

/// <summary>
/// Registra as policies de RBAC do Platform. Baseadas na claim de papel "user_role" (ver
/// JwtValidationExtensions.RoleClaimType) — nunca na claim "role" do Supabase.
/// SameTenant não é uma policy do ASP.NET Core: o isolamento por tenant já é garantido
/// pelo filtro global do EF Core + RLS (ver ADR-004), então não há necessidade de checar
/// "o recurso pertence ao tenant do JWT" de novo na camada de autorização.
/// </summary>
public static class PolicyExtensions
{
    public static IServiceCollection AddPlatformAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.OwnerOnly, policy => policy.RequireRole(UserRole.Owner.ToString()))
            .AddPolicy(Policies.PartnerOrAbove, policy => policy.RequireRole(
                UserRole.Owner.ToString(), UserRole.Partner.ToString()))
            .AddPolicy(Policies.LawyerOrAbove, policy => policy.RequireRole(
                UserRole.Owner.ToString(), UserRole.Partner.ToString(), UserRole.Lawyer.ToString()))
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(UserRole.Admin.ToString()))
            .AddPolicy(Policies.FinancialOnly, policy => policy.RequireRole(UserRole.Financial.ToString()))
            .AddPolicy(Policies.AnyAuthenticatedUser, policy => policy.RequireAuthenticatedUser());

        return services;
    }
}

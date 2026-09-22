namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Identidade do usuário autenticado na requisição atual, extraída do JWT do Supabase.
/// Implementação real (lendo HttpContext/ClaimsPrincipal) fica em Platform.Infrastructure.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? TenantId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}

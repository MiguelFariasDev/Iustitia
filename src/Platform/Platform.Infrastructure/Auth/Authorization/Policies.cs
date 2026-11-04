namespace Advocacia.Platform.Infrastructure.Auth.Authorization;

/// <summary>Nomes de policies de autorização — usar estas constantes em [Authorize(Policy = ...)] / RequireAuthorization(...).</summary>
public static class Policies
{
    public const string OwnerOnly = nameof(OwnerOnly);
    public const string PartnerOrAbove = nameof(PartnerOrAbove);
    public const string LawyerOrAbove = nameof(LawyerOrAbove);
    public const string AdminOnly = nameof(AdminOnly);
    public const string FinancialOnly = nameof(FinancialOnly);
    public const string AnyAuthenticatedUser = nameof(AnyAuthenticatedUser);
}

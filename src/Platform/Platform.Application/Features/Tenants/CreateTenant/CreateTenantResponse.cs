namespace Advocacia.Platform.Application.Features.Tenants.CreateTenant;

/// <param name="TenantId">Id do escritório criado.</param>
/// <param name="AdminUserId">Id do usuário Owner, igual ao id no Supabase Auth.</param>
/// <param name="RequiresEmailConfirmation">
/// Verdadeiro quando o projeto Supabase exige confirmação de e-mail: o escritório existe,
/// mas o Owner só consegue entrar depois de confirmar. A interface usa isso para mostrar
/// "confirme seu e-mail" em vez de mandar o usuário para um login que vai recusá-lo.
/// </param>
public sealed record CreateTenantResponse(Guid TenantId, Guid AdminUserId, bool RequiresEmailConfirmation);

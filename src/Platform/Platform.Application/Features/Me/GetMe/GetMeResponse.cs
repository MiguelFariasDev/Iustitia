namespace Advocacia.Platform.Application.Features.Me.GetMe;

/// <param name="UserId">Id do usuário (claim <c>sub</c> do JWT).</param>
/// <param name="Email">E-mail do JWT.</param>
/// <param name="TenantId">Escritório (claim <c>tenant_id</c>, emitida pelo Custom Access Token Hook).</param>
/// <param name="Roles">Papéis do JWT.</param>
/// <param name="Name">Nome completo, do cadastro do usuário. Nulo se o cadastro não foi encontrado.</param>
/// <param name="OabNumber">Número da inscrição na OAB (só dígitos). Nulo se não informado.</param>
/// <param name="OabState">UF da seccional da OAB (ex.: "SP"). Nulo se não informada.</param>
/// <param name="Specialty">Especialidade do advogado. Nula se não informada.</param>
public sealed record GetMeResponse(
    Guid? UserId,
    string? Email,
    Guid? TenantId,
    IReadOnlyCollection<string> Roles,
    string? Name = null,
    string? OabNumber = null,
    string? OabState = null,
    string? Specialty = null);

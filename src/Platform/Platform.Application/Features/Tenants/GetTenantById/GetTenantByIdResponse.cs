namespace Advocacia.Platform.Application.Features.Tenants.GetTenantById;

public sealed record GetTenantByIdResponse(
    Guid Id,
    string Name,
    string Cnpj,
    string Status,
    string? LogoUrl);

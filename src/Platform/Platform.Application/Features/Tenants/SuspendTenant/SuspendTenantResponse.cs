namespace Advocacia.Platform.Application.Features.Tenants.SuspendTenant;

public sealed record SuspendTenantResponse(Guid TenantId, string Status);

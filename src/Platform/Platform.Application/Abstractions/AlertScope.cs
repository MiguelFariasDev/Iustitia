using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Platform.Application.Abstractions;

/// <summary>Resolve (tenant, usuário) da requisição; sem os dois não há alerta a consultar.</summary>
internal static class AlertScope
{
    internal static bool TryResolve(ICurrentUser currentUser, out Guid tenantId, out Guid userId)
    {
        tenantId = currentUser.TenantId ?? Guid.Empty;
        userId = currentUser.UserId ?? Guid.Empty;

        return tenantId != Guid.Empty && userId != Guid.Empty;
    }
}

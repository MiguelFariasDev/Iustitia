using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.Modules.Workflow.Application.Features;

/// <summary>Resolve (tenant, usuário) da requisição; sem os dois não há tarefa nem evento a consultar.</summary>
internal static class WorkflowScope
{
    internal static bool TryResolve(ICurrentUser currentUser, out Guid tenantId, out Guid userId)
    {
        tenantId = currentUser.TenantId ?? Guid.Empty;
        userId = currentUser.UserId ?? Guid.Empty;

        return tenantId != Guid.Empty && userId != Guid.Empty;
    }
}

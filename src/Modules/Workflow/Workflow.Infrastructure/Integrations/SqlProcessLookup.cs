using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Workflow.Infrastructure.Integrations;

/// <summary>
/// Confirma o processo consultando a tabela `processos` por SQL, sem referenciar o assembly
/// de Legal (ver ADR-067). Passa pelo RLS da mesma conexão, então processo de outro escritório
/// nunca é encontrado; o tenant é repetido no WHERE como segunda camada.
/// </summary>
public sealed class SqlProcessLookup(WorkflowDbContext dbContext) : IProcessLookup
{
    public async Task<bool> ExistsAsync(Guid tenantId, Guid processId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM processos WHERE id = {processId} AND tenant_id = {tenantId} AND is_deleted = false) AS \"Value\"")
            .ToListAsync(cancellationToken);

        return rows.Count == 1 && rows[0];
    }
}

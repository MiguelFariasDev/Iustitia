namespace Advocacia.Modules.Workflow.Application.Abstractions;

/// <summary>
/// Porta para confirmar que um processo existe no escritório. Workflow não referencia Legal
/// (módulos não dependem uns dos outros — ADR-001); a implementação fica na infraestrutura
/// deste módulo (ver ADR-067).
/// </summary>
public interface IProcessLookup
{
    Task<bool> ExistsAsync(Guid tenantId, Guid processId, CancellationToken cancellationToken = default);
}

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>Remove mensagens antigas da outbox e registros expirados de idempotência (chamado pelo OutboxCleanupJob).</summary>
public interface IOutboxCleanupService
{
    /// <returns>Quantidade total de registros removidos.</returns>
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}

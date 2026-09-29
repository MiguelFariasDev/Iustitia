namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>Processa mensagens pendentes da outbox — chamado pelo OutboxProcessorJob (Hangfire, Hosts/Worker).</summary>
public interface IOutboxProcessor
{
    /// <returns>Quantidade de mensagens processadas com sucesso.</returns>
    Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);

    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>Processa mensagens pendentes da outbox — implementado pelo worker (Hosts/Worker).</summary>
public interface IOutboxProcessor
{
    Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}

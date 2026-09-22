using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;

/// <summary>
/// Lê mensagens pendentes da outbox e as marca como processadas. Nesta etapa, a leitura,
/// o corte em lote e a marcação de status já funcionam de verdade; a publicação real via
/// MassTransit/Azure Service Bus é implementada na Etapa 0.5, junto com a configuração
/// de mensageria — até lá, "processar" significa apenas marcar como concluído.
/// </summary>
public sealed class OutboxProcessor(EfDbContext dbContext) : IOutboxProcessor
{
    private const int BatchSize = 100;

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var pendingMessages = await dbContext.Set<OutboxMessage>()
            .Where(message => message.Status == OutboxStatus.Pending)
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                // TODO (Etapa 0.5): publicar via MassTransit/Azure Service Bus, resolvendo
                // o tipo concreto do evento a partir de message.EventType e desserializando
                // message.Payload antes de chamar IPublishEndpoint.Publish.
                message.MarkAsProcessed(DateTimeOffset.UtcNow);
            }
            catch
            {
                message.MarkAsFailed();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

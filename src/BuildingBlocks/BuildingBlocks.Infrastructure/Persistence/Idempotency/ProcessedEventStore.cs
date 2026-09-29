using Microsoft.EntityFrameworkCore;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Grava/consulta a tabela processed_events diretamente, fora da transação do handler que
/// originou o evento: um consumer de mensageria não tem uma UnitOfWork/transação ambiente,
/// então cada verificação/gravação de idempotência é sua própria operação atômica.
/// </summary>
public sealed class ProcessedEventStore(EfDbContext dbContext) : IProcessedEventStore
{
    public Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default) =>
        dbContext.Set<ProcessedEvent>()
            .AnyAsync(p => p.EventId == eventId && p.ConsumerName == consumerName, cancellationToken);

    public async Task MarkAsProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        dbContext.Set<ProcessedEvent>().Add(new ProcessedEvent(eventId, consumerName, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

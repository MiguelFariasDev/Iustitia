using System.Text.Json;
using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Antes de cada SaveChanges, coleta os eventos de domínio pendentes de todos os
/// agregados rastreados (<see cref="IHasDomainEvents"/>), serializa cada um em JSON e
/// insere um <see cref="OutboxMessage"/> na mesma transação (Outbox Pattern — ADR-008).
/// Os eventos são limpos do agregado logo em seguida, para não serem gravados de novo
/// em um SaveChanges futuro.
/// </summary>
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        InsertOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        InsertOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void InsertOutboxMessages(EfDbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregatesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var outboxMessage = new OutboxMessage(
                    aggregateId: ExtractAggregateId(aggregate),
                    eventType: domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                    payload: JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    createdAt: domainEvent.OccurredOn);

                context.Set<OutboxMessage>().Add(outboxMessage);
            }

            aggregate.ClearDomainEvents();
        }
    }

    /// <summary>
    /// AggregateRoot&lt;TId&gt; não expõe o Id de forma não-genérica em IHasDomainEvents,
    /// e os Ids do domínio são wrappers fortemente tipados (ex.: TenantId, UserId), não
    /// Guid puro. Reflection pontual aqui é o preço de manter o Outbox genérico sem
    /// acoplar BuildingBlocks a tipos concretos do domínio — só roda quando há eventos.
    /// </summary>
    private static Guid ExtractAggregateId(IHasDomainEvents aggregate)
    {
        var idValue = aggregate.GetType().GetProperty("Id")?.GetValue(aggregate);

        return idValue switch
        {
            Guid guid => guid,
            not null => idValue.GetType().GetProperty("Value")?.GetValue(idValue) as Guid? ?? Guid.Empty,
            null => Guid.Empty,
        };
    }
}

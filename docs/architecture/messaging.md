# Mensageria

Ver também: [ADR-008](../adr/008-outbox-pattern.md),
[ADR-016](../adr/016-masstransit-service-bus.md),
[ADR-034](../adr/034-outbox-pattern-masstransit.md),
[ADR-035](../adr/035-idempotencia-consumers.md).

## Fluxo completo

```
1. Handler de Command cria/modifica um agregado
2. Agregado chama RaiseDomainEvent(...)                    (AggregateRoot<TId>)
3. Handler chama IUnitOfWork.SaveChangesAsync()
4. OutboxInterceptor (SaveChangesInterceptor) serializa os domain events
   pendentes e insere um OutboxMessage por evento — MESMA transação
5. Transação commita: agregado + outbox_messages, atômico
6. OutboxProcessorJob (Hangfire, a cada minuto) chama
   IOutboxProcessor.ProcessPendingMessagesAsync
7. OutboxProcessor: lê lote Pending -> resolve tipo via IEventTypeResolver
   -> desserializa payload -> IPublishEndpoint.Publish (MassTransit)
8. MassTransit roteia para RabbitMQ (dev) ou Azure Service Bus (prod)
9. Consumer (ConsumerBase<TMessage>) verifica idempotência
   (IProcessedEventStore) -> HandleAsync -> marca como processado
10. OutboxCleanupJob (diário) remove outbox processada/falhada antiga
    e registros de idempotência expirados (30 dias)
```

## Como criar um novo integration event

1. Se o evento nasce de um `AggregateRoot<TId>` (ex.: um novo agregado do
   módulo Legal), crie um domain event normal (`record : DomainEvent`) e
   chame `RaiseDomainEvent(...)` no método de domínio — ele passa pela
   outbox automaticamente, sem código extra de mensageria.
2. Se o evento nasce de algo que **não** é `AggregateRoot` (ex.: `AuditLog`,
   que é write-once), crie um `record : IntegrationEvent(TenantId)` em
   `SeuModulo.Domain/.../Events` e publique diretamente via
   `IIntegrationEventPublisher.PublishAsync(...)` — best-effort, sem passar
   pela outbox.

## Como criar um novo consumer

```csharp
public sealed class MeuConsumer(
    IProcessedEventStore processedEventStore,
    ILogger<ConsumerBase<MeuEvento>> logger)
    : ConsumerBase<MeuEvento>(processedEventStore, logger)
{
    protected override Task HandleAsync(MeuEvento message, CancellationToken cancellationToken)
    {
        // lógica do consumer
    }
}
```

Para eventos que implementam `IIntegrationEvent` (têm `TenantId`), herde de
`IntegrationEventConsumer<TEvent>` em vez de `ConsumerBase<TEvent>` — o
TenantId já chega pronto no handler.

Registre o consumer em `Platform.Infrastructure.DependencyInjection`
(`AddAdvocaciaMessaging(configuration, bus => bus.AddConsumer<MeuConsumer>())`).
Módulos de negócio fazem o mesmo a partir da Fase 1+.

## Idempotência

Ver ADR-035. Resumo: todo consumer checa `EventId + ConsumerName` em
`processed_events` antes de processar; reentregas do mesmo evento pelo mesmo
consumer são ignoradas.

## Retry e dead-letter

`MassTransitConfiguration` aplica `UseMessageRetry` (5 tentativas,
exponencial, 2s–60s) a todos os endpoints. Mensagens que esgotam o retry vão
para a fila de erro do MassTransit (RabbitMQ, `<endpoint>_error`) ou a
dead-letter queue nativa do Azure Service Bus.

Na outbox (antes mesmo de chegar ao bus), falhas de publicação usam o mesmo
princípio: backoff exponencial via `NextRetryAt`, até `Outbox:MaxRetries`
(padrão 5), quando a mensagem vira `Failed` definitivamente.

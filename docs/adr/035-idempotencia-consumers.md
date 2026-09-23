# ADR-035: Idempotência em consumers

**Status:** Aceito — implementado na Etapa 0.5

## Contexto

O Outbox Pattern (ADR-008/034) garante entrega **at-least-once**, não
exactly-once: uma falha entre publicar e marcar `Processed`, ou um retry do
MassTransit, pode entregar a mesma mensagem mais de uma vez a um consumer.
Sem alguma proteção, `TenantCreatedConsumer` criaria settings/feature flags
duplicados, `UserInvitedConsumer` enviaria e-mails de convite repetidos, etc.

## Decisão

Toda mensagem tem um `EventId` (`Guid`, de `IDomainEvent`/`IIntegrationEvent`
— ver `BuildingBlocks.Domain.Abstractions`/`Events`). Antes de processar,
`ConsumerBase<TMessage>` (base de todo consumer, ver ADR-016) verifica na
tabela `processed_events` (`IProcessedEventStore`) se aquele `EventId` já
foi processado **por aquele consumer específico** (chave composta
`EventId + ConsumerName`, com índice único
`ix_processed_events_event_consumer`):

- Já processado → ignora (log de debug), não chama o handler de novo.
- Não processado → chama `HandleAsync`, e só marca como processado **depois**
  do handler concluir sem erro. Um erro no handler propaga (o MassTransit
  aplica retry/dead-letter — ver ADR-016) e a mensagem continua "não
  processada", será tentada de novo.

`processed_events` é uma tabela global (não filtrada por tenant), gravada
fora da transação do handler que originou o evento — um consumer de
mensageria não tem uma UnitOfWork/transação ambiente do jeito que um
handler de Command tem.

TTL de 30 dias: registros mais antigos são removidos pelo `OutboxCleanupJob`
(mesmo job que já limpa a outbox — ver ADR-017/034), assumindo que nenhuma
reentrega realista do transporte chega décadas depois.

## Consequências

**Positivas:**
- `IntegrationEventConsumer<TEvent>` (para eventos que implementam
  `IIntegrationEvent`) e consumers de domain event puro (`UserInvitedEvent`,
  `TenantCreatedEvent`) compartilham a mesma proteção de idempotência sem
  duplicar lógica — está toda em `ConsumerBase<TMessage>`.
- Testado de ponta a ponta (Etapa 0.5): reentrega do mesmo `EventId` para o
  mesmo consumer é ignorada (verificado via `HasBeenProcessedAsync` antes de
  `HandleAsync`).

**Negativas / trade-offs:**
- Race condition teórica: duas instâncias do Worker processando a mesma
  mensagem quase simultaneamente podem ambas passar pelo check antes de
  qualquer uma marcar como processada (não há lock pessimista). O índice
  único em `(EventId, ConsumerName)` pelo menos impede duplicar o *registro*
  de idempotência; o handler em si precisa ser tolerante a rodar 2x nesse
  cenário raro (aceitável para o volume/concorrência da Fase 0 — revisitar
  se algum handler feito idempotente-por-ledger não for idempotente por
  natureza).
- Mais uma tabela para manter/limpar — custo pequeno comparado ao risco de
  efeitos colaterais duplicados.

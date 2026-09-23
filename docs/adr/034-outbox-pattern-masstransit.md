# ADR-034: Publicação real da Outbox via MassTransit

**Status:** Aceito — implementado na Etapa 0.5

## Contexto

O ADR-008 estabeleceu o Outbox Pattern e deixou explícito que a publicação
real (passo "publicar via MassTransit/Azure Service Bus") ficaria para a
Etapa 0.5. Até então, `OutboxProcessor.ProcessPendingMessagesAsync` apenas
lia mensagens pendentes e as marcava como `Processed`, sem publicá-las de
verdade — suficiente para provar o padrão de persistência, insuficiente para
qualquer consumer real receber o evento.

## Decisão

Completar o `OutboxProcessor` para publicar de verdade, via
`IPublishEndpoint` do MassTransit (ADR-016):

1. Lê um lote (`Outbox:BatchSize`, padrão 100) de mensagens `Pending` cujo
   `NextRetryAt` já passou (ou é nulo).
2. Resolve o tipo concreto do evento a partir de `EventType` (nome completo
   do tipo, gravado pelo `OutboxInterceptor`) via `IEventTypeResolver` — que
   escaneia, sob demanda, os tipos de todos os assemblies carregados cujo
   **namespace** começa com `Advocacia` (o assembly em si chama-se só
   `Platform.Domain`, `BuildingBlocks.Domain`, etc. — a marca desacoplada,
   ADR-023, só existe no namespace, não no nome do assembly/projeto).
3. Desserializa o payload JSON para esse tipo e publica via
   `IPublishEndpoint.Publish(objeto, tipo, ct)`.
4. Sucesso → `MarkAsProcessed`. Falha → `MarkAsFailed(erro, próximoRetry,
   maxRetries)`, com backoff exponencial (`BackoffBaseSeconds * 2^tentativas`);
   ao esgotar `MaxRetries` (padrão 5), a mensagem vira `Failed`
   definitivamente (precisa de intervenção/replay manual).

Um `OutboxCleanupService` complementar remove mensagens `Processed` com mais
de `RetentionDays` (padrão 30) e `Failed` com mais de `RetentionDays * 2`,
rodando como `OutboxCleanupJob` (ADR-017).

## Consequências

**Positivas:**
- Fluxo completo verificado de ponta a ponta (Etapa 0.5): agregado levanta
  domain event → `OutboxInterceptor` grava na mesma transação →
  `OutboxProcessorJob` (Hangfire) lê e publica via RabbitMQ →
  `UserInvitedConsumer`/`TenantCreatedConsumer` recebem e processam.
- Falhas de publicação (ex.: transporte fora do ar) não perdem a mensagem —
  ficam `Pending` com backoff, tentadas de novo automaticamente.
- Nunca loga o payload completo (só `MessageId`/`EventType`/contagens) —
  reduz risco de vazar dado sensível em log.

**Negativas / trade-offs:**
- O `IEventTypeResolver` depende de todos os assemblies com domain events já
  terem sido carregados pelo runtime no momento do primeiro `Resolve()` —
  mitigado por ser `Lazy<T>` (computado sob demanda, não no construtor do
  singleton) e por, na prática, esses assemblies já estarem carregados assim
  que o primeiro `DbContext` é resolvido (referencia os tipos de domínio
  diretamente).
- Uma mensagem `Failed` definitivamente exige reprocessamento manual (não há
  UI de replay nesta etapa) — aceitável para o volume esperado na Fase 0.

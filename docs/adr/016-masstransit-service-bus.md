# ADR-016: MassTransit + Azure Service Bus para mensageria

**Status:** Aceito — implementado na Etapa 0.5

## Contexto

O Outbox Pattern (ADR-008) garante que nenhum domain event se perde, mas não
resolve como esses eventos chegam a outros consumidores (dentro do mesmo
processo Worker, e futuramente em módulos de negócio separados). Precisamos
de um bus de mensagens com: retry automático, dead-letter, roteamento por
tipo de mensagem e observabilidade — sem reinventar isso na mão em cima de
filas cruas.

Também precisamos rodar localmente sem depender de uma assinatura Azure real
durante o desenvolvimento.

## Decisão

Usar **MassTransit** como abstração de mensageria, com dois transportes
possíveis, escolhidos pela seção `Messaging:Transport` do appsettings (nunca
pelo ambiente ASP.NET Core diretamente — ver `MassTransitConfiguration`):

- **RabbitMQ** em desenvolvimento local, via container do `docker-compose`
  (`infra/docker/docker-compose.yml`, portas 5673/15673).
- **Azure Service Bus** em produção, via connection string injetada pelo
  Azure Key Vault.

> Nesta etapa (Fase 0), o `appsettings.json` base dos dois Hosts já vem com
> `Messaging:Transport = "RabbitMq"`, porque não existe ainda nenhum recurso
> Azure Service Bus provisionado (ver `infra/terraform`) — o dia em que a
> infra de produção for provisionada, o appsettings/Key Vault do ambiente de
> produção passa a sobrescrever para `"AzureServiceBus"` + a connection
> string real; nenhum código muda.

Todos os endpoints (um por consumer, nomeados em kebab-case a partir do nome
da classe) usam retry exponencial (5 tentativas, 2s a 60s) via
`UseMessageRetry`; mensagens que esgotam o retry vão para a fila de erro do
MassTransit (RabbitMQ) ou a dead-letter queue nativa (Azure Service Bus).

O `OutboxProcessor` publica cada `OutboxMessage` via `IPublishEndpoint`,
resolvendo o tipo concreto do evento a partir do `EventType` gravado pelo
`OutboxInterceptor` (ver `IEventTypeResolver`/`EventTypeResolver`).

## Consequências

**Positivas:**
- Trocar de transporte (RabbitMQ ↔ Azure Service Bus) é configuração, não
  código — `MassTransitConfiguration` é o único lugar que sabe a diferença.
- Retry e dead-letter são responsabilidade do MassTransit/transporte, não
  código próprio para reinventar.
- Testado de ponta a ponta contra RabbitMQ real (Etapa 0.5): outbox com
  eventos pendentes → `OutboxProcessorJob` → publicação → consumers
  (`UserInvitedConsumer`, `TenantCreatedConsumer`, `AuditLogCreatedConsumer`)
  recebem e processam via idempotência (ver ADR-035).

**Negativas / trade-offs:**
- Dois transportes para manter mentalmente (RabbitMQ em dev, Azure Service
  Bus em prod) — mitigado por serem intercambiáveis via configuração e por
  ambos exporem a mesma superfície (`IBusFactoryConfigurator`).
- Entrega é at-least-once: consumers precisam ser idempotentes (ADR-035).

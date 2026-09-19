# ADR-008: Outbox Pattern para eventos confiáveis

**Status:** Aceito — implementado na Etapa 0.2

## Contexto

Agregados de domínio levantam eventos (`TenantCreatedEvent`,
`UserInvitedEvent`, ...) que precisam eventualmente disparar efeitos
colaterais assíncronos: notificar outro módulo, enviar e-mail, publicar em
um bus de mensagens (MassTransit/Azure Service Bus — ADR-016). O problema
clássico é a consistência entre "salvar o estado" e "publicar o evento":
publicar diretamente dentro do mesmo método que persiste a mudança cria uma
janela onde ou o banco é atualizado mas a mensagem se perde (crash entre os
dois passos), ou a mensagem é publicada mas a transação do banco falha e
faz rollback — nos dois casos, o sistema fica inconsistente.

## Decisão

Implementar o **Transactional Outbox Pattern**:

1. Toda vez que um agregado (`AggregateRoot<TId>`, que implementa
   `IHasDomainEvents`) tem eventos pendentes, o **`OutboxInterceptor`**
   (`SaveChangesInterceptor` do EF Core, ver
   `BuildingBlocks.Infrastructure.Persistence.Interceptors`) roda **antes**
   de cada `SaveChanges`, serializa cada evento em JSON e insere um
   `OutboxMessage` na tabela `outbox_messages` — **na mesma transação** da
   mudança de estado que originou o evento. Depois, limpa os eventos do
   agregado (`ClearDomainEvents()`) para não gravá-los de novo.
2. Como a gravação do agregado e da mensagem de outbox são atômicas (mesma
   transação do EF Core), ou as duas acontecem, ou nenhuma acontece —
   nunca uma sem a outra.
3. Um worker (`OutboxProcessor`, hospedado em `Hosts/Worker`) lê
   periodicamente mensagens com `Status = Pending`, tenta publicá-las e
   marca como `Processed` ou `Failed` (com `RetryCount`). A publicação real
   via MassTransit/Azure Service Bus fica para a Etapa 0.5 — hoje o
   processor já lê, tenta e marca status de verdade, mas o passo de
   publicação é um `TODO` explícito.

## Consequências

**Positivas:**
- Garantia de que nenhum evento de domínio é perdido silenciosamente, sem
  precisar de um coordenador de transação distribuída (2PC).
- Testado de ponta a ponta contra Postgres real (ver
  `PlatformDbContextTests.SaveChanges_WhenAggregateHasDomainEvents_...`,
  Etapa 0.2): agregado com evento pendente → `SaveChangesAsync` → evento
  vira `OutboxMessage` na mesma transação → `DomainEvents` do agregado fica
  vazio.
- Índices dedicados (`ix_outbox_pending`, parcial em `status = 'Pending'`;
  `ix_outbox_status_created`) mantêm a leitura do worker eficiente mesmo
  com a tabela crescendo — ver `docs/database/indexes.md`.

**Negativas / trade-offs:**
- Entrega é **at-least-once**, não exactly-once: se o worker publicar mas
  falhar antes de marcar `Processed`, a mensagem pode ser reprocessada —
  consumidores downstream precisam ser idempotentes.
- Latência adicional entre "evento ocorreu" e "evento publicado" (o tempo
  até o worker rodar o próximo ciclo), aceitável para os casos de uso atuais
  (nenhum exige publicação em tempo real de milissegundos).
- Mais uma tabela e um índice a manter — custo pequeno comparado ao
  problema que resolve.

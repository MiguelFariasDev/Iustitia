# ADR-065 — Alertas in-app e notificação em tempo real

**Status:** aceito · **Contexto:** Etapa 1.5 (tela de publicações + alertas)

## Contexto

Quando uma publicação chega, o responsável pelo processo precisa ser avisado dentro do
sistema (sino, toast, painel de alertas). Alertas não são só de publicação — tarefa e
aniversário virão depois — e a ADR-055 proíbe os módulos de negócio (Legal) de depender de
`Platform.*`.

## Decisão

1. **`Alert` vive em Platform** (`Platform.Domain/Alerts`), genérico por tipo
   (`Publication`, `Task`, `Birthday`, `Other`). É pessoal: cada consulta filtra tenant **e**
   usuário; RLS cobre o tenant (`013_alerts.sql`).
2. **Legal cria alertas pela porta `IAlertPublisher`** (BuildingBlocks.Application), implementada
   em Platform.Infrastructure. Legal continua sem referenciar Platform. A criação é
   idempotente por `(usuário, tipo, referência)`, então reexecutar a captura não duplica.
3. **`CreateAlertFromPublication`** (Legal.Application) monta o texto do alerta só com número
   do processo, tipo e tribunal — **nunca** o inteiro teor da publicação.
4. **O job de captura trata o alerta como best-effort**: a publicação já está persistida; falha
   no alerta é logada e não derruba a captura.
5. **`AlertCreatedEvent` sai pela outbox** (ADR-008); `AlertCreatedConsumer` (Platform) publica
   no **Supabase Realtime Broadcast** pela API REST com a service role, no canal
   `tenant:{tenantId}:alerts:{userId}`.
6. **O canal é privado** (`"private": true`). A policy `alerts_realtime_receive` em
   `realtime.messages` só autoriza o tópico montado a partir do `tenant_id` e do `sub` do JWT.
   Sem isso qualquer cliente com a chave anon que conhecesse os ids escutaria os alertas.
7. **Realtime é conveniência, não requisito**: o resumo de alertas tem `refetchInterval` de
   60 s, então o sino converge mesmo sem canal. Falha de broadcast propaga ao consumer
   (retry do MassTransit), nunca ao usuário.
8. **Descartar não apaga** (`DismissedAt`): some das listagens, o registro fica para auditoria.

## Consequências

- Não há endpoint de criação de alerta: ninguém "inventa" um aviso pela API.
- Um novo tipo de alerta (tarefa, aniversário) só precisa chamar `IAlertPublisher`.
- O consumer de alertas está em Platform (não em Legal): o evento pertence ao agregado `Alert`.

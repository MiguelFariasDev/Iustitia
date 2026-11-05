# Módulo Atendimento — inbox e handoff do bot de WhatsApp

Decisões e justificativas: [ADR-067](../adr/067-modulo-atendimento-inbox-e-handoff.md).
Erros: [catálogo](../errors/catalog.md#atendimento-25002599).

## Fluxo

1. O cliente escreve no WhatsApp; a Meta chama o webhook do **Bot** (assinatura validada).
2. O Bot abre/recupera o atendimento no Core, registra a mensagem e consulta o status.
3. `Bot` → o Bot responde pelo fluxo automático e registra a resposta.
4. O cliente digita "2": o Bot chama `handoff`, envia "Aguarde, um advogado vai te atender." e
   passa a **não responder**.
5. O advogado vê a conversa em **Atendimentos** (status `PendingHuman`, "Aguardando"), assume
   (`Human`) e responde; o Core entrega ao Bot, que envia à Meta.
6. O advogado devolve ao bot (`Bot`) ou encerra (`Closed`).

## API do Bot — `X-Api-Key`

Base: `/api/v1/bot/atendimentos`. O escritório vem da chave. Todos os erros são ProblemDetails
(RFC 7807) com `errorCode`.

| Método e rota | Corpo | Resposta |
|---|---|---|
| `POST /` | `{ phoneNumber, clientId?, contactName? }` | `200 { atendimentoId, status, created }` — idempotente: devolve o aberto do telefone |
| `POST /{id}/mensagens` | `{ direction, sender, body, externalId? }` | `200 { messageId }` — idempotente por `externalId` |
| `POST /{id}/handoff` | — | `200 { success: true }` — idempotente |
| `GET /{id}/status` | — | `200 { atendimentoId, status }` |

`direction`: `Inbound` \| `Outbound`. `sender`: `Client` \| `Bot` (`Human` é recusado — só o
Core grava fala de humano). `status`: `Bot` \| `PendingHuman` \| `Human` \| `Closed`.

Regras que o Bot deve respeitar: consultar o status **antes** de responder; não responder em
`Human` nem em `PendingHuman` (exceto o aviso de espera); `409 ATENDIMENTO_BOT_BLOCKED` indica
que a regra foi violada. `401` = chave ausente, inválida ou revogada.

## API do advogado — JWT (`LawyerOrAbove`)

| Método e rota | Descrição |
|---|---|
| `GET /api/v1/atendimentos?status&assignedUserId&search&page&pageSize` | Fila. `search` casa nome do contato (ILIKE) ou trecho do telefone (3+ dígitos). `PendingHuman` e `Human` aparecem primeiro. |
| `GET /api/v1/atendimentos/{id}` | Detalhe |
| `GET /api/v1/atendimentos/{id}/mensagens?before&limit` | Histórico cronológico; `before` carrega as anteriores; `hasMore` indica mais páginas |
| `POST /api/v1/atendimentos/{id}/mensagens` `{ body }` | Envia. Assume a conversa se ainda não for do advogado. `200 { messageId, delivery }`; `500 ATENDIMENTO_DELIVERY_FAILED` se o Bot não entregou (a mensagem fica como `Failed`) |
| `PATCH /api/v1/atendimentos/{id}/status` `{ status }` | `Human` (assumir), `Bot` (devolver), `Closed` (encerrar). `204` |
| `PUT /api/v1/atendimentos/{id}/cliente` `{ clienteId }` | Vincula a um cliente de Legal. `204` |

## Chaves do Bot — JWT (`OwnerOnly`)

| Método e rota | Descrição |
|---|---|
| `POST /api/v1/bot/api-keys` `{ name }` | Emite. `201` com `apiKey` **em claro, uma única vez** |
| `GET /api/v1/bot/api-keys` | Lista (prefixo, uso, revogação — nunca o segredo) |
| `DELETE /api/v1/bot/api-keys/{id}` | Revoga |

**Rotação:** emitir nova → configurar no Bot → revogar a antiga.

## Core → Bot

`POST {Atendimento:Bot:BaseUrl}/api/v1/bot/send`, header `X-Api-Key: {Atendimento:Bot:ApiKey}`:

```json
{ "tenantId": "…", "atendimentoId": "…", "messageId": "…", "to": "5585999998888", "body": "Bom dia" }
```

Resposta esperada: `200 { "externalId": "wamid.…" }`. `messageId` é a chave de idempotência.

## Configuração do Core

| Chave | Descrição |
|---|---|
| `Atendimento:Bot:BaseUrl` | URL interna do Bot |
| `Atendimento:Bot:ApiKey` | Chave que o Bot espera do Core (Key Vault em produção) |

## Tempo real

Canal privado `tenant:{tenantId}:atendimentos`; eventos `atendimento.updated` e
`atendimento.message.created`, com **apenas ids e estado**. O front invalida as queries do
TanStack e busca o conteúdo pela API.

## Banco

Migration EF `AddAtendimentos` + `infra/supabase/migrations/014_atendimentos.sql` (RLS, privilégios
mínimos, índices trigram, policy do Realtime). Rollback: `infra/supabase/rollback/014_atendimentos.down.sql`.

# Módulo Atendimento — inbox e handoff do bot de WhatsApp

Decisões e justificativas: [ADR-067](../adr/067-modulo-atendimento-inbox-e-handoff.md) (handoff) e
[ADR-068](../adr/068-consulta-de-andamento-pelo-bot-verificacao-no-core.md) (consulta de andamento).
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

## Consulta de andamento pelo Bot — `X-Api-Key`

O cliente pergunta "como está meu processo?" no WhatsApp. **A identidade é verificada no Core**; o Bot
só repassa o que foi digitado e age conforme a `etapa`. Base:
`/api/v1/bot/atendimentos/{id}/consulta`. Limites e motivos: [ADR-068](../adr/068-consulta-de-andamento-pelo-bot-verificacao-no-core.md).

| Método e rota | Corpo | Resposta |
|---|---|---|
| `POST /iniciar` | — | `200 { etapa }` — `AguardandoDocumento`, `Negado` ou `Bloqueado` |
| `POST /verificar` | `{ documento }` (CPF/CNPJ, com ou sem pontuação) | `200 { etapa, processos?: [{ indice, titulo }] }` — `processos` só em `Verificado` (até 10) |
| `GET /processos/{indice}` | — | `200 { titulo, fase, ultimoAndamento, atualizadoEm }` |

`etapa`: `AguardandoDocumento` \| `Verificado` \| `Negado` \| `Bloqueado` \| `Expirada`.

**Regras**

- Só com o atendimento em `Bot`. Em `PendingHuman`, `Human` ou `Closed`: `409 ATENDIMENTO_CONSULTA_REQUER_BOT`.
- `iniciar` exige que o telefone seja de **um** cliente ativo do escritório. Sem cadastro (ou telefone
  compartilhado por dois clientes) responde `Negado`, igual a documento errado — não revela o motivo.
  Casamento de telefone: DDI 55, tolerando o nono dígito.
- Sessão de **10 min** a partir do início (não se estende ao verificar). `iniciar` de novo invalida a anterior.
- `verificar` normaliza o documento (só dígitos) e compara com o do cliente dono do telefone, em tempo constante.
  **3 falhas** bloqueiam o **cliente** por **30 min** (`Bloqueado`); as falhas são herdadas entre sessões
  numa janela de 30 min, então recomeçar não dá novas tentativas.
- Após `Verificado`: só processos **desse cliente neste escritório** (ids fixados na verificação; o detalhe
  lê por `tenant_id` + `cliente_id`). Índice fora da lista: `404 ATENDIMENTO_CONSULTA_PROCESSO_NOT_FOUND`.
  Sem verificação ativa: `403 ATENDIMENTO_CONSULTA_NAO_VERIFICADA` (recomeçar com `iniciar`).
- Conteúdo exposto: título (assunto + CNJ), fase (situação cadastrada), último andamento (tipo + data de
  publicação **revisada** por advogado) e data de atualização. Nunca peças, valores, parte contrária ou notas internas.

**Privacidade**

- O documento é comparado em memória e descartado: não vai a coluna, evento, auditoria nem log
  (`ToString` do comando e da requisição o mascaram). Telefone e andamento também não são logados.
- Mensagens do cliente durante a verificação (e até 60 s depois) têm o corpo substituído por
  `[mensagem omitida: verificação de identidade]` no histórico. O Bot deveria registrar um marcador ele mesmo.
- Auditoria (`atendimento_auditoria`): `ConsultaIniciada`, `ConsultaNegada`, `ConsultaVerificada`,
  `ConsultaBloqueada`, `ProcessoVisualizado` — ator = cliente, `reference_id` = processo visto, sem documento.

**Limitação:** CPF + telefone é fator de identidade fraco (SIM swap, CPF semi-público). Ver ADR-068 antes de
habilitar em produção; recomenda-se feature flag por escritório e segundo fator.

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

Consulta de andamento: migration EF `AddConsultaProcesso` + `015_atendimento_consulta.sql` (tabela
`atendimento_consulta_sessoes` com RLS, `UPDATE` por coluna, sem `DELETE`; índice de telefone em clientes).
Rollback: `infra/supabase/rollback/015_atendimento_consulta.down.sql`.

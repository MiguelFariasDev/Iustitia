# ADR-067 — Módulo Atendimento: inbox e handoff do bot de WhatsApp

**Status:** aceito · **Contexto:** integração Core ↔ Bot de WhatsApp (produtos e repositórios
separados, comunicação só por API REST)

## Contexto

O escritório atende clientes pelo WhatsApp por um **Bot** (repositório próprio, banco próprio,
única peça que fala com a Meta). Quando o cliente pede um humano, a conversa precisa aparecer
para o advogado no Core, ele assume, responde, e pode devolver ao bot. O Core é dono do estado
da conversa; o Bot consulta esse estado **antes de responder qualquer mensagem**.

## Decisão

### 1. Um bounded context novo, `Atendimento`

`src/Modules/Atendimento/{Domain,Application,Infrastructure,Api}`, com `AtendimentoDbContext`
próprio (mesmo banco físico — ADR-001). Não cabe em `Legal` (conversa de WhatsApp não é domínio
jurídico) nem em `Notifications` (inbox é bidirecional e tem estado).

A raiz de namespace é **`Advocacia.Modules.Atendimentos`** (plural). Com `…Modules.Atendimento`
a classe `Atendimento` seria sombreada pelo namespace de mesmo nome em qualquer código fora do
próprio namespace — o C# resolve o nome para o namespace primeiro.

### 2. Máquina de estados no agregado

```
Bot ──RequestHandoff──▶ PendingHuman ──AssignToHuman──▶ Human ──ReturnToBot──▶ Bot
 └──────────── AssignToHuman (advogado fala sem esperar o pedido) ─────────▶ ▲
qualquer estado aberto ──Close──▶ Closed (terminal)
```

- No máximo **um atendimento aberto por telefone e escritório** (índice único parcial
  `status <> 'Closed'`). Mensagem nova de um telefone encerrado abre outro atendimento.
- `RequestHandoff`, `AssignToHuman` (mesmo advogado) e o registro de mensagem com o mesmo
  `externalId` são **idempotentes**: o Bot pode repetir a chamada.
- Outro advogado tentando assumir uma conversa em `Human` recebe `409
  ATENDIMENTO_ALREADY_ASSIGNED` — dois advogados não digitam na mesma conversa sem saber um do
  outro. Para trocar de responsável, quem tem a conversa devolve ao bot.
- **O bot nunca fala com uma conversa em `Human`.** É regra do agregado
  (`ATENDIMENTO_BOT_BLOCKED`), não só convenção do Bot. Em `PendingHuman` o domínio ainda aceita
  uma fala do bot, e isso é deliberado: o fluxo pede o handoff **e depois** envia "Aguarde, um
  advogado vai te atender."; barrar essa mensagem tornaria o fluxo do prompt original
  impossível. O silêncio do fluxo automático em `PendingHuman` é responsabilidade do Bot, que
  consulta o status antes de responder.

### 3. Duas credenciais, dois mundos

| Quem | Como autentica | Acessa |
|---|---|---|
| Advogado | JWT do Supabase | `/api/v1/atendimentos/*` (`LawyerOrAbove`) |
| Dono do escritório | JWT do Supabase | `/api/v1/bot/api-keys/*` (`OwnerOnly`) |
| Bot | `X-Api-Key` | `/api/v1/bot/atendimentos/*` |

- A chave identifica o **escritório** (uma chave ativa por tenant em uso normal); o tenant nunca
  vem do corpo da requisição. Só o **hash SHA-256** é guardado; a chave em claro aparece uma
  vez, na emissão. Rotação = emitir nova, trocar no Bot, revogar a antiga.
- A identidade do Bot carrega só `tenant_id`: sem `sub` e sem papel, de modo que nenhuma policy
  de advogado a aceita, e o JWT de um advogado não passa no esquema do Bot (testado nos dois
  sentidos).
- O tenant do Bot é definido num **endpoint filter** (`BotTenantEndpointFilter`), não no
  `TenantContextMiddleware`: este roda antes da autorização e só enxerga o JWT, e o tenant mora
  num `AsyncLocal` que só vale para o que roda dentro da chamada que o definiu.
- `bot_api_keys` **não tem RLS**: a chave é buscada pelo hash antes de o tenant ser conhecido.
- O limitador de taxa global particiona por IP (e roda antes da autenticação); o Bot, que é um
  cliente HTTP único atendendo todos os contatos, ganhou partição própria **por hash da chave**
  e limite maior (1200/min).
- Core → Bot (`POST {Atendimento:Bot:BaseUrl}/api/v1/bot/send`) usa **outra** chave
  (`Atendimento:Bot:ApiKey`), independente da que o Bot usa para chamar o Core.

### 4. Envio do advogado: persistir antes, entregar depois

`POST /atendimentos/{id}/mensagens` grava a mensagem (`delivery = Pending`) **antes** de chamar o
Bot, e só então atualiza para `Sent` (com o `wamid`) ou `Failed`. A mensagem do advogado nunca se
perde se o Bot estiver fora do ar, e não há transação de banco aberta durante uma chamada HTTP.
O gateway **não faz retry**: o reenvio é decisão do advogado, que vê a mensagem marcada como
falha. O `messageId` segue ao Bot como chave de idempotência.

> Divergência consciente do prompt original, que dizia "o Bot recebe, envia à Meta **e salva no
> Core**". Com o Core já gravando a mensagem do advogado, o Bot gravá-la de novo a duplicaria. O
> Bot devolve o `wamid` na resposta do `send` e o Core o associa à mensagem que já tem.

### 5. Tempo real sem dado pessoal

- Eventos de domínio (`AtendimentoIniciado`, `StatusAlterado`, `MensagemAdicionada`,
  `ClienteVinculado`) atravessam a outbox e **não carregam telefone nem texto**. Um teste de
  arquitetura impede acrescentar essas propriedades.
- Os consumers publicam no canal `tenant:{tenantId}:atendimentos` (Supabase Realtime Broadcast,
  `private: true`) **só ids e estado**; o front, ao receber o aviso, busca o conteúdo pela API
  autenticada (que passa por RLS). Mesmo com a policy do canal mal configurada, nada sigiloso
  vaza. A policy `atendimentos_realtime_receive` restringe o canal a `Owner/Partner/Lawyer` do
  próprio tenant.
- `IRealtimeNotifier`/`RealtimeChannels` **moveram de `Platform.Application` para
  `BuildingBlocks.Application`**: módulo de negócio não pode depender de `Platform.*` (ADR-055),
  e o Atendimento precisa publicar em tempo real. Para que módulos registrem consumers no mesmo
  bus (o `AddMassTransit` só pode ser chamado uma vez), `AddPlatformInfrastructure` ganhou o
  gancho opcional `configureModuleConsumers`.
- Realtime é conveniência, não requisito (ADR-065): sem canal, a fila converge no refetch.

### 6. Privilégios mínimos no banco (`014_atendimentos.sql`)

`002_rls.sql` concede CRUD a **toda** tabela nova via `ALTER DEFAULT PRIVILEGES`; por isso o
script faz `REVOKE ALL` antes de conceder o mínimo:

- **Auditoria** (`atendimento_auditoria`): só `SELECT` e `INSERT` — imutável até para a aplicação.
- **Mensagens**: `UPDATE` apenas em `delivery` e `external_id`. O corpo nunca é reescrito.
- **Atendimentos**: `UPDATE` por coluna, sem `tenant_id`, `phone_number` e `created_*`.
- Nenhuma tabela de conversa tem `DELETE` — o histórico é prova de atendimento e fica retido
  pelo prazo da política (5 anos). Exclusão por LGPD é procedimento próprio, não um `DELETE` da
  aplicação.

Consequência de código: **handlers deste módulo não chamam `Repository.Update`.** A entidade já
vem rastreada e o EF detecta as colunas alteradas; `DbSet.Update` marcaria todas e o `UPDATE`
passaria a tocar colunas sem permissão. `AtendimentoAsAppUserTests` roda o fluxo completo
conectado como `app_user` justamente para pegar isso.

### 7. Auditoria

Toda ação de estado grava uma linha em `atendimento_auditoria` (quem — cliente, bot ou usuário —,
o quê, de/para qual status, quando). Fica no módulo, porque `IAuditService` mora em `Platform.*`.

## Consequências

- O Bot depende de um contrato HTTP estável (`docs/modules/atendimento.md`); mudança de contrato
  exige versão nova (`/api/v2`), como qualquer API do sistema.
- Conteúdo de mensagens é dado pessoal e sigiloso profissional, hoje **em texto** na coluna
  `body`. Criptografia em nível de coluna (pgcrypto/Supabase Vault) é a evolução natural e está
  registrada como pendência.
- O vínculo conversa ↔ cliente é manual (`PUT /atendimentos/{id}/cliente`). Casar o telefone com
  o cadastro de Legal exigiria o módulo de atendimento conhecer Legal; fica como porta futura.

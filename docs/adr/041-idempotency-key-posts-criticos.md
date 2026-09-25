# ADR-041: Idempotency-Key em POSTs críticos

**Status:** Aceito — implementado na Etapa 0.7

## Contexto

Um cliente pode enviar o mesmo POST duas vezes por motivos legítimos: timeout
de rede seguido de retry automático, duplo clique, app mobile reenviando
após reconectar. Para a maioria dos GETs isso é inofensivo (idempotentes por
natureza), mas para POSTs que criam recursos — criar um escritório, convidar
um usuário, aceitar um convite — um retry sem proteção cria duplicatas.

## Decisão

`IdempotencyMiddleware` (Platform.Api), aplicado só aos POSTs críticos
(`/tenants`, `/users/invite`, `/users/accept-invite` — lista em
`CriticalPathSuffixes`):

1. Header `Idempotency-Key` ausente → 400 (`VALIDATION_REQUIRED_FIELD`).
2. Header presente, chave nunca vista → calcula o hash SHA-256 do corpo da
   requisição, executa o handler normalmente, e (só em caso de sucesso,
   2xx) grava no Redis (`idempotency:{key}`, TTL 24h) o hash do corpo +
   status + content-type + corpo da resposta.
3. Header presente, chave já vista, **mesmo hash de corpo** → não executa o
   handler de novo: responde com a mesma resposta gravada (mesmo status e
   corpo), com o header `Idempotency-Replayed: true` para o cliente saber
   que não foi uma nova execução.
4. Header presente, chave já vista, **hash de corpo diferente** → 409
   (`COMMON_CONFLICT`) — a mesma chave não pode representar duas operações
   diferentes; isso normalmente indica um bug no cliente (reusar uma chave
   entre requisições distintas).

Respostas de falha (4xx/5xx) **não** são gravadas: se o handler falhar
(ex.: validação), o cliente precisa poder corrigir o payload e tentar de
novo com a mesma chave, sem ficar preso a uma resposta de erro cacheada.

O middleware roda depois de Authentication/TenantContext/Authorization (ver
ordem do pipeline, ADR-039/docs/api/conventions.md) — a chave de
idempotência é sempre avaliada com tenant/usuário já resolvidos.

## Consequências

**Positivas:**
- Testado de ponta a ponta (Etapa 0.7, `IdempotencyTests`): sem header →
  400; mesma chave + mesmo corpo → replay idêntico (`Idempotency-Replayed`);
  mesma chave + corpo diferente → 409.
- Cliente pode fazer retry seguro (timeout de rede, reconexão) sem risco de
  duplicar um tenant/convite.
- TTL de 24h no Redis é suficiente para cobrir qualquer janela realista de
  retry, sem acumular chaves indefinidamente.

**Negativas / trade-offs:**
- O corpo da resposta é bufferizado inteiro em memória (`MemoryStream`)
  para poder ser gravado no cache — aceitável para os payloads pequenos
  destes POSTs específicos (nunca upload de arquivo), mas o middleware
  não deveria ser aplicado a um endpoint de resposta grande sem revisar
  esse ponto.
- A lista de paths críticos é hardcoded (`CriticalPathSuffixes`) — cada
  novo POST crítico (ex.: `/processes/import`, Fase 2) precisa lembrar de
  ser adicionado à lista; não há nenhum mecanismo automático (ex.: um
  atributo `[Idempotent]`) que force isso. Aceitável para o número pequeno
  de POSTs críticos hoje; revisitar se a lista crescer muito.

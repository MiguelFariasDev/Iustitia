# ADR-069 — Auditoria e privacidade no uso de IA

**Status:** aceito · **Contexto:** Fase 3, Etapa 3.1

## Contexto

Publicações são comunicações processuais: contêm dados pessoais e estão sob **sigilo
profissional** (Estatuto da OAB) e **LGPD**. Ao mesmo tempo, o claude.md (seção 16) exige logar
toda chamada à Anthropic para auditoria de uso e custo. As duas exigências se conciliam
registrando **fatos sobre a chamada, nunca o conteúdo dela**.

## Decisão

1. **Regra de ouro: o conteúdo nunca é registrado.** Nem o prompt, nem o system prompt, nem a
   resposta do modelo, nem a `message` de erro da Anthropic (que pode repetir trecho do pedido)
   vão para banco, log de sistema, span, métrica ou mensagem de erro. Aplicada em camadas:
   - **estrutural**: `AiUsageLog` e `AnthropicUsageLogEntry` não têm campo capaz de guardar texto
     (um teste falha se alguém adicionar um); a tabela não tem coluna para isso;
   - **comportamental**: testes com WireMock provam que prompt, system prompt, resposta e
     mensagem de erro não aparecem em nenhum log;
   - **de erro**: o cliente lê só o `type` do erro da Anthropic (ex.: `rate_limit_error`).
2. **`ai_usage_logs`** (tabela do `AnthropicDbContext`; RLS por tenant — ADR-004): `id`,
   `tenant_id`, `user_id` (nulo em job), `model`, `purpose`, `prompt_version`, `reference_id` (ex.:
   a publicação), os quatro contadores de tokens, `cost_usd`, `latency_ms`, `success`,
   `error_code`, `request_id`, `created_at`. Uma linha por chamada, **append-only** (a aplicação
   só tem `SELECT, INSERT`). CHECKs: `success` ⇔ sem `error_code`; valores não negativos.
3. **Custo por tenant**: `cost_usd = Σ tokens × tarifa / 1.000.000`, com as tarifas de
   `Anthropic:Pricing` (entrada, saída, escrita e leitura de cache), 6 casas decimais. Somando
   `cost_usd` por `tenant_id` obtém-se o gasto de cada escritório. Modelo sem tarifa registra 0 e
   avisa; os **tokens são a fonte de verdade** para reconciliar com a fatura da Anthropic.
4. **Quem audita**: o cliente HTTP **não** grava auditoria (não sabe tenant, usuário nem
   finalidade); o chamador registra via `IAnthropicUsageLogger`. Isso permite registrar também o
   **desfecho de negócio**: uma resposta que chegou mas foi descartada pela validação é gravada
   como `success = false`, `error_code = AI_INVALID_RESPONSE` — foi cobrada e precisa constar.
5. **Falha de auditoria propaga**: uma chamada paga sem registro é pior que uma operação que falha
   e é retentada.
6. **Contexto separado**: a auditoria usa seu próprio `SaveChanges`/DbContext, então não arrasta
   alterações pendentes de outro módulo e não é desfeita por rollback dele.
7. **Métricas** (`anthropic.*`) têm rótulos só de modelo, sucesso e código de erro — nunca tenant
   ou usuário (cardinalidade e privacidade).

## Consequências

- (+) Dá para responder "quanto cada escritório gastou?" e "quantas respostas inválidas?" sem
  tocar em dado sigiloso; e para provar ao cliente o que foi (e o que não foi) registrado.
- (+) O mesmo registro alimenta a regra anti-laço da 3.2 (parar de reprocessar quem falha sempre).
- (−) Depurar uma resposta ruim exige reproduzir a chamada: não há conteúdo guardado. É o preço do
  sigilo; para diagnóstico existe o `request_id`, que a Anthropic consegue rastrear.
- (−) O tipo `AnthropicMessage.Content` carrega conteúdo em memória durante a chamada — dever de
  quem consome não o repassar a logs.

# Integração com a Anthropic (Claude)

Módulo `Integrations.Anthropic` (Fase 3, Etapa 3.1). Decisões: ADR-067 (integração),
ADR-068 (prompt caching), ADR-069 (auditoria e privacidade). Uso na classificação de
publicações: [ai-classification.md](../modules/legal/ai-classification.md).

## Uso

```csharp
var result = await anthropicClient.SendMessageAsync(
    prompt, systemPrompt, new AnthropicOptions { Model = "claude-sonnet-5-5", MaxTokens = 2048 }, ct);

if (result.IsFailure) { /* result.Error.Code: AI_RATE_LIMITED, AI_TIMEOUT, ... */ }

// Quem chama sabe tenant/usuário/finalidade; o cliente não audita sozinho:
await usageLogger.LogAsync(new AnthropicUsageLogEntry(
    tenantId, userId, result.Value.Model, result.Value.Usage, latencyMs, Success: true,
    Purpose: "classification", ReferenceId: publicationId, RequestId: result.Value.RequestId), ct);
```

Tool use: `SendMessageWithToolsAsync(messages, tools, ct)`; se `response.RequestsToolUse`, ler
`response.ToolUses` (`Id`, `Name`, `InputJson`). Não há ferramentas concretas ainda (Fase 5).

## Configuração (`Anthropic:*`)

| Chave | Padrão | |
|---|---|---|
| `BaseUrl` | `https://api.anthropic.com/v1/` | |
| `ApiKey` | — | **nunca em arquivo**: `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."` (dev); Key Vault `Anthropic--ApiKey` (produção) |
| `DefaultModel` / `DefaultMaxTokens` | `claude-sonnet-5-5` / 4096 | usados quando a chamada não informa |
| `Effort` | `medium` | `output_config.effort`; vazio omite |
| `TimeoutSeconds` / `MaxRetries` / `RetryBaseDelaySeconds` | 60 / 3 / 2 | backoff 2s, 4s, 8s com jitter |
| `CircuitBreaker*` | razão 0,5 · mín. 5 · janela 120s · pausa 60s | janela ≥ 2× o timeout (regra do Polly) |
| `Models:*` | — | modelo por finalidade (Etapa 3.2) |
| `Pricing:<modelo>` | — | USD por milhão de tokens: `InputPerMillion`, `OutputPerMillion`, `CacheWritePerMillion`, `CacheReadPerMillion` |

## Erros (faixa AI, 1600–1699)

Ver `docs/errors/catalog.md`. Resumo: 400 → `AI_INVALID_REQUEST` · 401/403 → `AI_UNAUTHORIZED` ·
429 → `AI_RATE_LIMITED` · 529 → `AI_OVERLOADED` · timeout → `AI_TIMEOUT` · 5xx/rede/breaker →
`AI_UNAVAILABLE` · corpo ilegível → `AI_PARSE_ERROR`.

## Observabilidade

Span `anthropic.send_message` (fonte `Advocacia.Anthropic`); métricas `anthropic.tokens.input`,
`anthropic.tokens.output`, `anthropic.cost.usd`, `anthropic.requests.duration` (rótulos: `model`,
`success`, `error_code`). Nada de conteúdo.

## Auditoria

Tabela `ai_usage_logs` (migration EF `InitialCreate` do `AnthropicDbContext` + `015_ai_usage_logs.sql`
com RLS, CHECKs e grants). Custo por escritório no mês:

```sql
SELECT tenant_id, sum(cost_usd) AS usd, sum(input_tokens) AS tin, sum(output_tokens) AS tout
  FROM ai_usage_logs WHERE created_at >= date_trunc('month', now()) GROUP BY tenant_id;
```

## Testes

`tests/UnitTests/Integrations.Anthropic.UnitTests`: cliente contra WireMock (200, 400, 401, 429 com
retry, 529, 5xx, timeout, JSON inválido, tool use, cache_control, headers, nada sigiloso em log),
cálculo de custo e logger de uso.

# Classificação de publicações e sugestão de prazo por IA

**Status:** implementado na Fase 3, Etapa 3.2. A **revisão humana** (aprovar/corrigir/rejeitar a
sugestão) e a criação de tarefas/fila são as Etapas 3.3 e 3.4 — **nada aqui altera a publicação**.
Decisões: ADR-070 (fluxo e validação), ADR-071 (prompts); a integração com a Anthropic está nas ADR-067 a 069 e em [anthropic.md](../../integrations/anthropic.md).

## Fluxo

```
PublicationClassificationJob  (Hangfire, a cada 15 min, por escritório)
  └─ publicações PendingReview SEM sugestão, mais antigas primeiro
       ├─ IClassificationService.ClassifyAsync   → tipo do ato + prazo inicial   (prompt classification.md)
       └─ IDeadlineSuggestionService.SuggestAsync → prazo refinado para esse tipo  (prompt deadline.md)
            └─ PublicationSuggestion  → publication_suggestions  (confiança = menor das duas)
Cada chamada (inclusive a inválida) → ai_usage_logs
```

- **Validação:** `AiResponseParser` rejeita qualquer resposta fora do contrato (ver ADR-070). Erro
  `AI_INVALID_RESPONSE`; o log cita só o nome do campo, nunca o conteúdo.
- **Confiança < 50** ⇒ `requires_manual_review = true` ("pendente de revisão manual").
- **Falha do provedor** (indisponível, timeout, limite, credencial) ⇒ a execução para e relança
  para o retry do Hangfire (3 tentativas: 1, 5 e 15 min).
- **Resposta inválida repetida** ⇒ após 3 tentativas a publicação deixa de ser reprocessada
  (cada tentativa é cobrada) e segue na fila **sem sugestão**.
- Prazo em **horas** não é suportado: o modelo responde sem prazo.

## Prompts

`src/Modules/Workflow/Workflow.Infrastructure/Prompts/{classification,deadline}.md`, com `version:`
no front matter. **Mudou o texto ⇒ suba a versão e rode a regressão.** Tipos de ato permitidos
(lista fechada, igual em `ActTypes` e no prompt): `despacho`, `decisao_interlocutoria`, `sentenca`,
`acordao`, `decisao_monocratica`, `intimacao`, `citacao`, `audiencia`, `edital`, `outros`.

## Configuração

| Chave | Padrão | O quê |
|---|---|---|
| `Anthropic:ApiKey` | — | **Nunca em arquivo**: user-secrets em dev; Key Vault `Anthropic--ApiKey` em produção |
| `Anthropic:Models:Classification` / `:Deadline` | `claude-sonnet-5-5` | modelo de cada finalidade |
| `Anthropic:Models:MaxOutputTokens` | 4096 | teto de saída (o raciocínio conta) |
| `Anthropic:Models:MaxContentCharacters` | 16000 | corte do texto enviado (marca `content_truncated`) |
| `Anthropic:Effort` | `medium` | `output_config.effort` |
| `Anthropic:Pricing:<modelo>` | — | USD por milhão de tokens (entrada, saída, escrita e leitura de cache) |
| `Jobs:PublicationClassification:Enabled` / `CronExpression` | `true` / `*/15 * * * *` | agendamento |
| `…:BatchSizePerTenant` | 20 | publicações por escritório por execução |
| `…:MaxInvalidResponseAttempts` | 3 | respostas inválidas toleradas por publicação |
| `…:TenantIds` | `[]` | restringe a escritórios específicos (vazio = todos com pendência) |

> O modelo `claude-3-5-sonnet-20241022` (do pedido original da etapa) foi aposentado pela
> Anthropic; a API o recusa. Veja ADR-067.

## Tabelas

- **`publication_suggestions`** (módulo Legal, migration EF `AddPublicationSuggestions` + `014_publication_suggestions.sql`)
  — uma por publicação (`ux_publication_suggestions_publication`): `act_type`,
  `suggested_deadline_days`, `deadline_type` (`business`/`calendar`), `legal_basis`, `confidence`,
  `requires_manual_review`, `content_truncated`, `ai_provider`, `ai_model`, `prompt_version`,
  `raw_response` (jsonb com as respostas validadas), `created_at`. RLS por tenant.
- **`ai_usage_logs`** (módulo Anthropic — ver [anthropic.md](../../integrations/anthropic.md) e ADR-069)
  — uma linha por chamada, com `purpose = classification|deadline`, `prompt_version` e
  `reference_id` = a publicação. Resposta cobrada mas descartada fica como `success = false`,
  `error_code = AI_INVALID_RESPONSE`; é essa contagem que limita as tentativas por publicação.
  **Append-only** e sem conteúdo.

## Operação

Consultas úteis:

```sql
-- custo por escritório no mês
SELECT tenant_id, sum(cost_usd) AS usd, sum(input_tokens) AS tin, sum(output_tokens) AS tout
  FROM ai_usage_logs WHERE created_at >= date_trunc('month', now()) GROUP BY tenant_id;

-- taxa de resposta inválida por modelo e versão de prompt
SELECT model, prompt_version, status, count(*) FROM ai_usage_logs GROUP BY 1, 2, 3 ORDER BY 1, 2;
```

## Regressão de prompt (manual — custa dinheiro)

```bash
ANTHROPIC_API_KEY=... dotnet test --filter Category=PromptRegression
# outro modelo: ANTHROPIC_MODEL=...
```

20 chamadas reais (10 de classificação, 10 de prazo) em `tests/RegressionTests/Prompts`. Sem a
chave os testes são pulados. **Não rodam no CI**; quando houver pipeline de testes, excluir a
categoria (`--filter "Category!=PromptRegression"`). Os textos são sintéticos (ver ADR-071).

## Limites conhecidos

- A IA **não consulta calendário**: não há feriados, suspensões nem contagem de data final. O
  prazo é número de dias + forma de contagem; a data é problema da revisão/agenda (fases seguintes).
- Publicações de processos sem vínculo cadastrado são classificadas sem área do direito.
- Não há endpoint nem tela para ver a sugestão ainda (Etapa 3.3).

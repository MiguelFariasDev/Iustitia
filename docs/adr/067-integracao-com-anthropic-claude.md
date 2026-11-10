# ADR-067 — Integração com a Anthropic (Claude): cliente HTTP, resiliência e erros

**Status:** aceito · **Contexto:** Fase 3, Etapa 3.1

## Contexto

A Fase 3 introduz IA no fluxo de publicações. Antes de qualquer regra de negócio (Etapa 3.2),
precisamos de uma fundação para falar com a Anthropic de forma resiliente, segura e auditável.
A escolha do provedor já está no claude.md (seção 2) e na ADR-013; esta ADR registra **como** o
módulo `Integrations.Anthropic` foi construído.

## Por que Claude

- **Qualidade em português jurídico** para classificar atos e identificar prazos, que é o núcleo da
  Fase 3.
- **Contrato de não-treinamento**: dados de API não treinam modelos por padrão (ADR-014), requisito
  de sigilo profissional (OAB) e LGPD.
- **Prompt caching** (ADR-068), que reduz custo e latência em prompts repetitivos como o nosso.
- **Tool use** nativo, que a Athena (Fase 5) vai usar — o cliente já nasce com o contrato pronto.

## Decisão

1. **Estrutura** (Clean Architecture, ADR-001/005): `Integrations.Anthropic.Domain` (modelos puros:
   `AnthropicMessage`, `AnthropicUsage`, `AnthropicResponse`, `AnthropicTool`),
   `.Application` (portas `IAnthropicClient`, `IAnthropicUsageLogger`, `IAnthropicUsageQuery`,
   `IAiCostCalculator`, `IPromptCatalog`; DTOs de fio) e `.Infrastructure` (cliente HTTP, Polly,
   observabilidade, persistência da auditoria). Módulos de negócio dependem **só das portas**.
2. **Cliente tipado sobre `IHttpClientFactory`**, sem Refit: é uma única rota (`POST v1/messages`),
   sem ganho que justifique outra dependência (claude.md: "adicionar dependência exige
   justificativa"). Headers `x-api-key`, `anthropic-version: 2023-06-01`, `content-type: json`.
3. **Resiliência (Polly 8 via `AddStandardResilienceHandler`)**: timeout total > circuit breaker >
   retry > timeout por tentativa. Retry (padrão 3, backoff exponencial 2s/4s/8s com jitter) em
   408/429/5xx/529/timeout/erro de rede; **4xx não é retentado** (erro nosso; repetir só gasta
   quota). Timeout por tentativa de 60s.
4. **Mapeamento de erros** para a faixa **AI (1600–1699)** do catálogo, sempre via `ErrorFactory`
   e `Result`: 400/404/413/422 → `AI_INVALID_REQUEST`; 401/403 (ou chave ausente) →
   `AI_UNAUTHORIZED`; 429 → `AI_RATE_LIMITED`; 529 → `AI_OVERLOADED`; 408/504/timeout →
   `AI_TIMEOUT`; demais 5xx, rede e circuit breaker aberto → `AI_UNAVAILABLE`; corpo ilegível →
   `AI_PARSE_ERROR`. `AI_INVALID_RESPONSE` e `AI_PROMPT_NOT_FOUND` são da Etapa 3.2. Só o
   cancelamento pelo chamador propaga como exception.
5. **Tool use preparado**: `AnthropicRequestDto` aceita `Tools`/`ToolChoice` e a resposta
   interpreta blocos `tool_use` (`AnthropicResponse.ToolUses`). `SendMessageWithToolsAsync` usa
   `tool_choice: auto`. Nenhuma ferramenta concreta existe (Fase 5).
6. **Observabilidade**: `ActivitySource "Advocacia.Anthropic"` (span `anthropic.send_message`) e
   métricas `anthropic.tokens.input`, `anthropic.tokens.output`, `anthropic.cost.usd`,
   `anthropic.requests.duration` no Meter `Advocacia`, ambos registrados na configuração central
   do OpenTelemetry. Spans, métricas e logs (Serilog) carregam só metadados — ver ADR-069.
7. **Segredo**: `Anthropic:ApiKey` só por user-secrets (dev) / Key Vault `Anthropic--ApiKey`
   (produção). O appsettings versionado tem a chave vazia.

## Desvios do desenho original da etapa (e por quê)

- **Modelo**: o pedido trazia `claude-3-5-sonnet-20241022`, **aposentado** pela Anthropic (a API o
  recusa com 404). O padrão é `claude-sonnet-5-5` (claude.md: "Sonnet 4.5 ou mais recente"),
  configurável em `Anthropic:DefaultModel` e `Anthropic:Models`.
- **Circuit breaker "5 falhas em 30s"**: o pipeline padrão do Polly exige janela de amostragem ≥ 2×
  o timeout por tentativa. Com timeout de 60s a janela mínima é 120s. Mantivemos 5 chamadas mínimas
  e razão de falha 0,5, em janela de 120s.
- **`temperature`**: existe no DTO, mas só é enviada se o chamador pedir. Modelos atuais rejeitam
  valores não padrão; a previsibilidade vem do prompt e da validação (ADR-070).
- **Numeração**: o pedido pedia ADRs 066–068, mas a 066 já existe (telas de apoio). Usamos 067–069.

## Consequências

- (+) Uma porta estável: trocar de provedor ou migrar para o SDK oficial não toca o Legal.
- (+) Falhas de IA viram `Result` previsível; o job do Legal decide o que é transitório.
- (−) Cliente próprio é código a manter (acompanhar mudanças da Messages API).
- (−) `AnthropicMessage.Content` é texto; blocos estruturados (`tool_result`, imagens) exigirão
  estender o tipo na Fase 5.

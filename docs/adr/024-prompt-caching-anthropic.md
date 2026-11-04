# ADR-024: Prompt caching da Anthropic para reduzir custo

**Status:** Aceito (implementado na Etapa 3.2)

## Contexto

Classificar publicações repete, em toda chamada, o mesmo system prompt longo (papel, lista de
tipos de ato, regras de prazo, formato de saída); só o texto da publicação muda.

## Decisão

O system prompt é enviado como bloco `text` com `cache_control: {"type": "ephemeral"}`
(`AnthropicRequest.CacheSystemPrompt`, padrão `true`). O conteúdo variável fica sempre depois do
trecho cacheado. Os tokens de escrita e de leitura de cache são lidos de `usage`, precificados à
parte (`Anthropic:Pricing`) e gravados em `ai_usage_logs` — a taxa de acerto do cache é
verificável por `cache_read_input_tokens`.

## Consequências

- (+) Menos custo e latência nas chamadas em lote do job.
- (−) O cache só vale acima de um tamanho mínimo de prefixo e por tempo limitado; um prompt curto
  ou chamadas espaçadas não se beneficiam. Qualquer alteração no system prompt invalida o cache
  (esperado: ele é versionado — ADR-068).

# ADR-068 — Uso de Prompt Caching

**Status:** aceito · **Contexto:** Fase 3, Etapa 3.1 (detalha a ADR-024)

## Contexto

A classificação de publicações repete, em toda chamada, o mesmo system prompt longo (papel, lista
de tipos de ato, regras de prazo, formato de saída). Só o texto da publicação muda. Sem cache,
pagamos esses tokens de entrada a cada chamada, a preço cheio.

## Como funciona

A Anthropic permite marcar um bloco do prompt com `cache_control: {"type": "ephemeral"}`. A
primeira chamada **grava** o prefixo (até aquele bloco) no cache; as seguintes, dentro da validade
(minutos), **leem** do cache. A ordem de renderização é `tools` → `system` → `messages`, e o cache
é por **prefixo exato**: qualquer byte diferente antes do ponto marcado invalida o resto.

Cobrança por categoria de token (os preços reais ficam em `Anthropic:Pricing`):

| Token | Tarifa relativa à entrada |
|---|---|
| Entrada normal | 1× |
| Escrita de cache (`cache_creation_input_tokens`) | ≈ 1,25× |
| Leitura de cache (`cache_read_input_tokens`) | ≈ 0,1× |

Logo, a partir da segunda chamada o system prompt custa cerca de **10%** do normal, e a latência
cai. Há um tamanho **mínimo** de prefixo para o cache valer (centenas a milhares de tokens,
conforme o modelo): prompts curtos simplesmente não são cacheados, sem erro.

## Decisão

1. O `system` do `AnthropicRequestDto` é uma **lista de blocos** (e não string) justamente para
   poder levar `cache_control`. `AnthropicOptions.CacheSystemPrompt` (padrão `true`) liga/desliga
   por chamada.
2. O **conteúdo variável fica sempre fora do trecho cacheado**: a publicação vai na mensagem do
   usuário, nunca no system prompt. Prompts com data/ID por requisição no system quebrariam o cache.
3. Os quatro contadores de tokens são lidos de `usage`, **precificados à parte** e gravados em
   `ai_usage_logs`. A taxa de acerto é verificável: `cache_read_input_tokens` > 0 nas chamadas
   seguintes. Se ficar em zero, há um invalidador silencioso (ex.: prompt mudando entre chamadas).
4. Os prompts são versionados (ADR-071): alterar o system prompt invalida o cache — esperado.

## Consequências

- (+) Redução relevante de custo e latência no processamento em lote do job.
- (−) O cache expira em minutos: chamadas muito espaçadas (ex.: um escritório com poucas
  publicações) pagam a escrita sem colher a leitura. Aceitável: o volume pequeno também custa pouco.
- (−) Quem editar o prompt precisa saber que isso zera o cache.

# ADR-072 — Geração de peças com RAG, Ollama na Vast.ai e GPU sob demanda

Status: proposto (código não compilado nem executado).

## Contexto
Escritórios precisam de minutas assistidas por IA com fundamentação em acervo próprio, custo controlado e sigilo.

## Decisão
- **Assíncrono:** `POST /pecas/gerar` grava `Pendente`, enfileira e retorna 202. Um worker processa.
- **Fila no banco** (sem Redis), reserva por UPDATE condicional com lease, retry só para falhas transitórias, dead-letter em `Falhou`.
- **GPU por demanda:** `GpuOrchestrator` liga a instância Vast.ai só com trabalho, espera Ollama saudável e modelos, e a **para** (nunca destrói)
  após ociosidade. Circuit breaker e idempotência nas chamadas.
- **RAG:** BGE-M3 via Ollama, Qdrant (`juridico_chunks`), filtro obrigatório de tenant e visibilidade, ~8.000 tokens de contexto.
- **Streaming** por polling do texto parcial no banco (funciona entre processos).
- **Acesso a dados:** Qdrant só via `QdrantService`; o frontend nunca recebe URL, chave ou modelo.

## Alternativas rejeitadas
Chamada síncrona na API (GPU ligada ou requisição longa); Redis/Hangfire dedicado para a fila (dependência extra); Ollama público.

## Consequências e riscos
- Dados do cliente passam por host de terceiro: exige avaliação LGPD/OAB e contrato.
- Latência de partida fria (minutos) — mitigada com status por etapas.
- Modelo 8B pode alucinar: revisão humana obrigatória e verificação de citações.
- `NumCtx` precisa ser ≥ 16384 (8192 truncaria o prompt).
- Código não verificado por compilador/teste; migração EF pendente.

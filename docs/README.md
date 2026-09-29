# Documentação — Iustitia

Índice da documentação técnica do repositório. Para contexto de negócio e
regras não-negociáveis, comece por [`/claude.md`](../claude.md).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| [`adr/`](./adr) | Architecture Decision Records — decisões arquiteturais e o porquê de cada uma |
| [`api/`](./api) | Documentação da API REST pública (OpenAPI/Swagger, gerada a partir da Fase 0/5) |
| [`architecture/`](./architecture) | Visão de arquitetura, camadas e pipeline de request |
| [`database/`](./database) | Schema, índices, performance e estratégia de migrations |
| [`frontend/`](./frontend) | Aplicação web Next.js — estrutura, design system, estado, performance e acessibilidade |
| [`errors/`](./errors) | Catálogo de erros, convenções de código de erro e como adicionar um novo |
| [`integrations/`](./integrations) | Integrações externas: comportamento observado, limitações e recomendações — [`cnj.md`](./integrations/cnj.md) |
| [`modules/`](./modules) | Documentação por módulo de negócio (bounded context) — [`legal/`](./modules/legal) a partir da Fase 1 |
| [`poc/`](./poc) | Provas de conceito: relatório executivo, métricas medidas e lições aprendidas |
| [`observability/`](./observability) | Logging, tracing, métricas e health checks |
| [`compliance/`](./compliance) | LGPD, conformidade OAB, contrato de não-treinamento com a Anthropic |
| [`navigation/`](./navigation) | Sitemaps web e mobile mantidos sincronizados com o código real |
| [`diagrams/`](./diagrams) | Diagramas de arquitetura, fluxo e modelo de dados |
| [`requisitos/`](./requisitos) | Requisitos detalhados do produto por módulo/necessidade (não versionado — uso interno, ver `.gitignore`) |

## Por onde começar

1. `claude.md` — regras não-negociáveis, stack, convenções, fases.
2. `docs/adr/001-modular-monolith.md` a `005-clean-architecture-vertical-slice.md`
   — decisões fundacionais já documentadas com contexto completo.
3. `docs/compliance/` — antes de tocar qualquer fluxo de IA ou dado pessoal.
4. `docs/requisitos/00-indice.md` — requisitos funcionais completos por
   módulo (dashboard, processos, workflow, kanban, athena, etc.).

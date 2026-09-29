# ADR-045: Skills da Vercel como guia de performance e composição

**Status:** Aceito — adotado na Etapa 0.8

## Contexto

Regras de performance em React/Next.js (evitar waterfalls, controlar o
bundle, não re-renderizar à toa) são conhecidas, mas se perdem na revisão de
código: ninguém lembra de checar trinta itens a cada PR. O resultado típico
é um app que degrada aos poucos, sem um commit culpado.

A Vercel publica esse conhecimento como *agent skills* — conjuntos de regras
legíveis por agentes e por humanos.

## Decisão

Adotar as skills da Vercel como referência normativa ao escrever e revisar
código do frontend. As instaladas no ambiente e efetivamente usadas:

- **`vercel-react-best-practices`** — 70 regras em 8 categorias, por impacto:
  waterfalls (`async-*`), bundle (`bundle-*`), servidor (`server-*`), fetch
  no cliente (`client-*`), re-render (`rerender-*`), renderização
  (`rendering-*`), JavaScript (`js-*`) e padrões avançados.
- **`vercel-composition-patterns`** — composição sobre configuração:
  compound components, slots em vez de proliferação de props booleanas.
- **`web-design-guidelines`** — auditoria de interface e acessibilidade
  (foco, formulários, tipografia, movimento, toque, i18n).

Aplicações concretas, rastreáveis no código:

| Regra | Onde |
|---|---|
| `server-parallel-fetching`, `async-suspense-boundaries` | prefetch no servidor + `Suspense` nas páginas de dados |
| `bundle-dynamic-imports` | Devtools do React Query e `CommandPalette` via `next/dynamic` |
| `bundle-barrel-imports` | `optimizePackageImports` + imports diretos, sem barrel files |
| `rerender-derived-state` | `useOnboardingHint` assina um booleano derivado |
| `rerender-functional-setstate` | seleção/expansão nas tabelas via `setState` funcional |
| `js-combine-iterations`, `js-index-maps` | filtro em uma passada; `Map` dia→eventos na `WeekGrid` |
| `rendering-conditional-render` | ternários (`? :`), nunca `&&`, no JSX |

**Importante — divergência do ambiente:** o prompt da etapa citava cinco
skills. Apenas três existem instaladas (`npx skills list -g`);
`next-best-practices` e `next-cache-components` **não estão disponíveis**.
As convenções do App Router e de cache foram aplicadas a partir da
documentação oficial do Next.js, e não a partir dessas skills. Se elas forem
publicadas, revisar `docs/frontend/performance.md`.

## Consequências

**Positivas:**
- Critério de revisão explícito e compartilhado, em vez de preferência
  pessoal de quem revisa.
- As regras têm prefixo e nome (`async-parallel`, `bundle-barrel-imports`),
  o que permite citá-las em comentário de código e em PR.
- `web-design-guidelines` transformou-se em correções reais nesta etapa:
  `touch-action: manipulation`, `overscroll-behavior: contain` em overlays,
  `scroll-margin-top` em âncoras e "Desfazer" no descarte de alerta.

**Negativas / trade-offs:**
- Skills são externas e versionadas fora do repositório: podem mudar ou sair
  do ar. Por isso este ADR registra *quais* regras foram aplicadas e onde —
  a decisão não depende da skill continuar existindo.
- Nem toda regra se aplica: "Title Case para títulos e botões" é convenção
  do inglês; em pt-BR o produto usa caixa de sentença. Divergências assim
  são registradas em `docs/frontend/accessibility.md`, não silenciadas.

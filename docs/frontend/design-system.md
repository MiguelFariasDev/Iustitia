# Design System

Referência dos tokens e componentes da interface. Decisão e justificativa em
[ADR-043](../adr/043-shadcn-tailwind-design-system.md); referências de
layout em [ADR-058](../adr/058-referencias-visuais-concorrencia.md).

**Regra número um: nunca use um hex direto num componente.** Toda cor sai de
um token semântico definido em `src/app/globals.css`.

## Cores

Definidas em `:root` (claro) e `.dark` (escuro), expostas ao Tailwind via
`@theme inline`.

### Marca

| Token | Claro | Uso |
|---|---|---|
| `--primary` | `#0A2540` | Azul-marinho: seriedade, confiança |
| `--secondary` | `#2563EB` | Azul elétrico: ação primária, links |
| `--gold` | `#C9A961` | Dourado: destaque pontual, nunca texto sobre branco |

### Feedback

| Token | Claro | Uso |
|---|---|---|
| `--success` / `--success-foreground` | `#10B981` / `#047857` | Confirmação |
| `--warning` / `--warning-foreground` | `#F59E0B` / `#B45309` | Atenção, prazo próximo |
| `--destructive` / `--destructive-foreground` | `#EF4444` / `#B91C1C` | Erro, prazo vencido |
| `--info` / `--info-foreground` | `#3B82F6` / `#1D4ED8` | Informação neutra |

### A armadilha dos sufixos `-foreground`

Os dois grupos têm o mesmo sufixo e significados **opostos**:

- `--primary-foreground` / `--secondary-foreground` são a cor do texto
  **sobre o preenchimento sólido** (branco sobre azul).
- `--success-foreground`, `--warning-foreground`, `--destructive-foreground`
  e `--info-foreground` são tons **escuros**, pensados para texto sobre
  **fundo suave** (`bg-success/10`), como em `components/shared/tones.ts`.

Usar `text-secondary-foreground` sobre `bg-secondary/12` produz texto branco
sobre fundo quase branco. Foi exatamente a violação que o axe-core apontou
nos blocos da Agenda nesta etapa.

> Sobre fundo translúcido da marca, use `text-foreground`.

### Superfícies e sidebar

`--background`, `--card`, `--popover`, `--muted`, `--accent`, `--border`,
`--input`, `--ring`. A barra lateral tem escala própria (`--sidebar*`)
porque é marinho no tema claro e quase preto no escuro.

## Tipografia

Carregada com `next/font` no root layout — sem FOUT, sem requisição a
terceiros.

| Família | Token | Uso |
|---|---|---|
| Inter | `--font-sans` | Interface |
| Playfair Display | `--font-serif` | Logo e marca |
| JetBrains Mono | `--font-mono` | Números de processo, dados técnicos |

Utilitário `tabular` (`font-variant-numeric: tabular-nums`) em toda coluna
de números, horário e contador, para os dígitos não "dançarem".

## Espaçamento, raio, sombra e movimento

- Escala de 4px (padrão do Tailwind).
- `--radius: 0.625rem`, com `sm`/`md`/`lg`/`xl`/`2xl` derivados.
- Sombras sutis (`--shadow-xs` a `--shadow-lg`) — elevação, não drama.
- `--ease-standard: cubic-bezier(0.2, 0, 0, 1)`, duração padrão 150ms.
- `prefers-reduced-motion: reduce` zera animações e transições globalmente.

`src/config/theme.ts` espelha os tokens em TypeScript para usos fora do CSS
(`theme-color`, futuros gráficos). O CSS continua sendo a fonte da verdade.

## Componentes compartilhados

`src/components/shared/` — componentes do produto, sem regra de negócio:

| Componente | Função |
|---|---|
| `Logo` | Monograma + wordmark, versão normal e invertida |
| `PageHeader` | `h1` + descrição + ações |
| `EmptyState` | Ícone, título, descrição e ação |
| `LoadingSpinner` | `role="status"`; `decorative` para uso dentro de botões |
| `ErrorBoundary` / `ErrorFallback` / `QueryBoundary` | Erro e carregamento |
| `ConfirmDialog` | Confirmação de ação destrutiva |
| `DataTable` | Tabela com ordenação, filtro e paginação no cliente |
| `Pagination`, `SearchInput` | Paginação e busca com debounce |
| `StatusBadge`, `PriorityBadge`, `DeadlineBadge` | Selos por tom |
| `MetricCard` | Métrica com variação e mini gráfico opcional |
| `MiniBarChart` | Tendência em CSS puro, sem biblioteca de charts |
| `OnboardingTooltip` | Aviso de primeiro acesso, dispensa persistida |
| `HighlightBanner` | Faixa de destaque para novidades |
| `ComingSoon` | Placeholder de módulo de fase futura |

### `LoadingSpinner` e a regra do `role="status"`

Por padrão o spinner é uma região `status` com rótulo. **Dentro de um
botão**, use `decorative`: o botão já anuncia o próprio estado por
`aria-busy`, e uma segunda região `status` competiria com a da página.

## Cor nunca é o único indicador

Todo selo, bloco de agenda e badge de status carrega **texto** e, quando
aplicável, **ícone**. A cor reforça; não informa sozinha (WCAG 1.4.1).

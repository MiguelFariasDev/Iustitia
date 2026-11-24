# Design system Iustitia (azul + Athena)

Substitui a paleta verde/violeta do protótipo v4 (`design-system.md` e `v4-alinhamento.md` descrevem a versão anterior).
Fonte da verdade: o artefato "Iustitia design system" (`tokens.json`). Os tokens vivem em `web/src/app/globals.css`.

## Mapeamento de nomes

| Design system | Token no código | Uso |
|---|---|---|
| `surface-100` / `surface-000` / `surface-200` | `background` / `card`, `popover`, `sidebar` / `muted`, `accent` | página / cards e navegação / cabeçalho de tabela e hover |
| `line-100` / `line-200` | `border` / `line-strong`, `input` | divisória fina / borda de controle (≥3:1) |
| `ink-900` / `ink-600` / `ink-500` | `foreground` / `muted-foreground` / `sidebar-muted` | título / corpo / rótulo |
| `brand-600`, `brand-700`, `brand-050`, `on-brand` | `primary` (= `brand`, `secondary`), `brand-strong`, `brand-soft`, `primary-foreground` | **cor de ação**: botão primário, links, item ativo, foco |
| `athena-600`, `athena-050`, `athena-300` | `ia`/`ia-text`, `ia-soft`, `ia-border` | **exclusiva da IA** (Athena). Nunca para sucesso ou estados genéricos |
| `danger-*`, `warning-*`, `success-*` | `destructive`, `warning`, `success` (+ `-soft`) | prazo fatal, não tratada/prazo próximo, tratada. Sempre com palavra ou ícone |

No tema escuro os mesmos nomes mudam de valor; nunca escreva hex em componente.

## Tipografia

`font-serif` = Source Serif 4 (só display: `display-xl` hero, `display-l` seções da landing, `display-m` saudação e CTA);
`font-sans` = Public Sans (toda a interface); `font-mono` = IBM Plex Mono (número CNJ, OAB, CPF/CNPJ).
Utilitários em `globals.css`: `display-xl`, `display-l`, `display-m`, `iu-label` (caixa alta), `iu-metric`.

## Forma e elevação

Raios: 6px selos, 8px botões/inputs, 12px cards (`rounded-xl`), 20px painel do hero (`rounded-2xl`).
`shadow-sm` = card (sempre com borda); `shadow-lg` = só o que flutua.

## Marca

Sem símbolo oficial: o `Logo` é o wordmark (nome em serifa 600 + ponto em `brand`). Não desenhar balança nem martelo.

## Landing

`web/src/features/landing` (conteúdo em `content.ts`). Hero, Como funciona, Recursos, Athena IA, Segurança, Para quem,
Perguntas e CTA, nessa ordem. Sem gradiente, sem foto de banco de imagens, sem número, depoimento ou garantia de resultado.
O mockup do hero usa dados fictícios e diz isso na legenda.

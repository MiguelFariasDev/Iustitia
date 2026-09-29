# ADR-043: shadcn/ui + Tailwind CSS 4 para o Design System

**Status:** Aceito — implementado na Etapa 0.8

## Contexto

O produto precisa de um vocabulário visual consistente (botões, campos,
tabelas, diálogos, badges) com acessibilidade correta — foco visível, ARIA,
navegação por teclado — sem que o time reimplemente cada primitiva.

As opções consideradas foram: (a) uma biblioteca de componentes fechada
(MUI, Ant Design), (b) primitivas sem estilo + CSS próprio, (c) shadcn/ui.

O produto tem identidade visual própria e não pode parecer "app genérico de
biblioteca X". Ao mesmo tempo, o time é pequeno demais para escrever do zero
um combobox acessível.

## Decisão

**shadcn/ui** (estilo *new-york*, base *slate*, CSS variables) sobre **Radix
UI** + **Tailwind CSS 4**.

O ponto central: shadcn/ui **não é uma dependência**, é código copiado para
`src/components/ui/`. O comportamento acessível vem do Radix; a aparência é
nossa e versionada no repositório.

**Tokens semânticos, não cores literais.** `src/app/globals.css` define as
variáveis em `:root` (claro) e `.dark` (escuro) e as expõe ao Tailwind via
`@theme inline`. Os componentes usam `bg-card`, `text-muted-foreground`,
`border-input` — nunca um hex. Trocar o tema é editar um bloco de variáveis.

Paleta: azul-marinho (`--primary: #0A2540`) para seriedade, azul elétrico
(`--secondary: #2563EB`) para ação, dourado (`--gold: #C9A961`) como
destaque pontual, mais os tons de feedback (sucesso, atenção, erro, info) e
uma escala de sidebar própria.

**Convenção dos sufixos `-foreground`.** `--secondary-foreground` é a cor do
texto *sobre o preenchimento sólido* (branco sobre azul). Já
`--success-foreground`, `--warning-foreground`, `--info-foreground` e
`--destructive-foreground` são tons escuros pensados para *fundos suaves*
(`bg-success/10`), como em `tones.ts`. Confundir os dois grupos produz texto
branco sobre fundo quase branco — foi exatamente o que a auditoria axe-core
pegou nos blocos da Agenda. Regra prática: sobre fundo translúcido da marca,
use `text-foreground`.

`src/config/theme.ts` espelha os tokens em TypeScript para os usos fora do
CSS (`<meta name="theme-color">`, futuros gráficos em canvas). A fonte da
verdade continua sendo o CSS.

## Consequências

**Positivas:**
- Acessibilidade de base vem pronta do Radix (foco, ARIA, teclado, portais),
  e a auditoria WCAG 2.1 AA com axe-core passa em 12 rotas sem violações.
- Componentes são editáveis: `CardTitle`, `Alert` e `LoadingSpinner` foram
  ajustados no próprio repositório quando a semântica exigiu, sem fork nem
  `!important`.
- Tema claro/escuro sai de graça: o mesmo componente responde às variáveis.

**Negativas / trade-offs:**
- Atualizar shadcn/ui é manual: não há `npm update` para código copiado. As
  customizações precisam ser reaplicadas a cada geração.
- Os arquivos gerados mantêm o estilo upstream (ex.: `transition-all`, que
  as diretrizes de interface desaconselham). São tratados como baseline do
  fornecedor e isolados em `src/components/ui/**`, com regra de ESLint
  própria.
- Classes Tailwind longas reduzem a legibilidade do JSX. Mitigado por
  `prettier-plugin-tailwindcss` (ordenação estável) e por extrair variantes
  com `cva` quando passam de um punhado.

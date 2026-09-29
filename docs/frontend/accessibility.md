# Acessibilidade

Meta do produto: **WCAG 2.1 nível AA**. Não é aspiracional — é verificado
automaticamente a cada execução do E2E.

## Verificação automática

`web/e2e/accessibility.spec.ts` roda axe-core (`@axe-core/playwright`) com
as tags `wcag2a`, `wcag2aa`, `wcag21a` e `wcag21aa` em 12 rotas: painel,
publicações, alertas, agenda, um placeholder, três telas de configurações,
login, esqueci-senha, onboarding e a 404. Inclui uma passagem no tema
escuro e a verificação do skip link.

O teste falha com **qualquer** violação — não há lista de exceções.

```bash
npm run test:e2e -- e2e/accessibility.spec.ts
```

## O que a auditoria já pegou

Casos reais corrigidos nesta etapa, registrados aqui porque tendem a se
repetir:

1. **Contraste nos blocos da agenda.** Usavam `text-secondary-foreground`
   (branco, feito para preenchimento sólido) sobre `bg-secondary/12` — texto
   branco sobre fundo quase branco, 7 nós reprovados. Corrigido para
   `text-foreground`. Ver a armadilha dos sufixos `-foreground` em
   [design-system.md](./design-system.md).
2. **Título de card sem papel de cabeçalho.** `CardTitle` do shadcn/ui é uma
   `div`; os títulos de seção do painel não eram navegáveis por cabeçalho.
   Passaram a conter um `<h2>` real, como já ocorria nas configurações.
3. **`role="alert"` em conteúdo estático.** A dica de modo demonstração no
   login era anunciada assertivamente no carregamento da página. Passou a
   `role="note"`.
4. **Duas regiões `status` competindo.** O spinner dentro de um botão criava
   uma segunda região `status` que disputava com a da página. Passou a
   `decorative`, e o botão anuncia o estado por `aria-busy`.
5. **Ação destrutiva sem volta.** "Descartar alerta" era imediato. Ganhou
   "Desfazer" no toast.

## Convenções

### Estrutura

- Um `<h1>` por página, sempre via `PageHeader`.
- Landmarks: `header`, `nav`, `main`, `aside`. `main` tem `id="conteudo"` e
  `tabIndex={-1}` para receber o skip link.
- Skip link é o primeiro elemento focável do documento.
- Seções com `aria-labelledby` apontando para o próprio cabeçalho.

### Controles

- Botão só de ícone precisa de `aria-label` ou texto `sr-only`.
- Quando o rótulo visível se repete na tela ("Descartar alerta" em vários
  cards), use `aria-label` explícito com o contexto. Texto visível + `span`
  `sr-only` produz espaçamento de nome acessível diferente entre jsdom e
  navegador — `aria-label` é determinístico.
- `<button>` para ação, `<a>`/`<Link>` para navegação. Nunca `div` com
  `onClick`.
- Estado de expansão em `aria-expanded` + `aria-controls` apontando para o
  id da região.
- Estado de filtro em `aria-pressed`; item de navegação ativo em
  `aria-current="page"`.

### Foco

- `:focus-visible` com `outline-2 outline-offset-2 outline-ring` global.
- `outline-none` só é aceitável acompanhado de substituto `focus-visible`,
  ou em alvos de foco programático (`tabIndex={-1}`).

### Conteúdo dinâmico

- Contadores que mudam sozinhos usam `aria-live="polite"`.
- Toasts (sonner) já têm região live própria.
- Skeletons são `role="status"` com `aria-label` começando em "Carregando".

### Decorativo é decorativo

- Todo ícone que acompanha texto leva `aria-hidden`.
- A régua de horas da agenda é `aria-hidden`: cada evento já anuncia o
  próprio horário; uma lista solta de horas só polui o leitor de tela.
- `MiniBarChart` é `role="img"` com `aria-label` descritivo — a tendência
  precisa ser descrita, não lida ponto a ponto.

### Cor, movimento e toque

- Cor nunca é o único indicador: sempre há texto e, quando cabe, ícone.
- `prefers-reduced-motion: reduce` zera animações e transições.
- `touch-action: manipulation` em controles (remove o atraso de 300ms).
- `overscroll-behavior: contain` em diálogos, gavetas e popovers.

## Divergência consciente das diretrizes

As diretrizes de interface da Vercel pedem **Title Case** em títulos e
botões (convenção do inglês, estilo Chicago). O produto é pt-BR e usa
**caixa de sentença**, que é a norma da língua. Regra não aplicada.

## Verificação manual

O automatizado não cobre tudo. Antes de dar uma tela como pronta:

- Percorra a tela inteira só com `Tab`/`Shift+Tab`: a ordem faz sentido? O
  foco fica sempre visível? Nada fica preso?
- `Esc` fecha diálogos e a paleta de comandos.
- Em zoom de 200%, nada some nem gera rolagem horizontal.
- Leitor de tela (VoiceOver/NVDA): os cabeçalhos descrevem a página quando
  lidos em sequência?

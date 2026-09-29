# Performance

Regras aplicadas e onde elas aparecem no código. Base normativa:
[ADR-045](../adr/045-vercel-skills-react-nextjs.md).

## Configuração

`web/next.config.ts`:

- `reactCompiler: true` — memoização automática; `useMemo`/`useCallback`
  ficam para os casos que o compilador não cobre.
- `typedRoutes: true` — rota inexistente quebra o build.
- `optimizePackageImports: ['lucide-react', 'radix-ui', 'date-fns']` —
  reescreve imports de barrel para imports diretos.
- `distDir` configurável por `NEXT_DIST_DIR` — o E2E compila em `.next-e2e`
  para não invalidar o `.next` de um `next dev` em andamento.

## Eliminar waterfalls

- Páginas de dados fazem `prefetchQuery` no servidor e entregam via
  `HydrationBoundary`: o HTML já chega preenchido e o cliente não refaz o
  fetch (`server-parallel-fetching`).
- `<Suspense>` em volta do componente que busca dados: o `PageHeader` é
  enviado imediatamente e o miolo faz streaming
  (`async-suspense-boundaries`).
- `connection()` antes de buscar dados por tenant/usuário: marca a rota como
  dinâmica e evita pré-renderizar em build dados que são por requisição.

## Bundle

- `next/dynamic` para o que não é necessário no primeiro paint: Devtools do
  React Query (só em desenvolvimento) e `CommandPalette`
  (`bundle-dynamic-imports`).
- Imports diretos, sem barrel files internos (`bundle-barrel-imports`).
- `MiniBarChart` é CSS puro: nenhuma biblioteca de gráficos entra no bundle
  por causa de uma tendência de 40px de altura.
- `features/agenda/week.ts` implementa a aritmética de semana à mão, em vez
  de adicionar `date-fns` ao cliente por seis funções.

## Re-render

- Estado derivado durante o render, nunca em `useEffect`
  (`rerender-derived-state-no-effect`).
- Seletores derivados no Zustand: `useOnboardingHint` assina um booleano, não
  o array de avisos (`rerender-derived-state`).
- `setState` funcional onde o próximo valor depende do anterior — seleção e
  expansão de linhas nas tabelas (`rerender-functional-setstate`).
- Ternário no JSX, nunca `&&` (`rendering-conditional-render`): evita
  renderizar `0` por acidente.
- `'use no memo'` em `DataTable`: a TanStack Table devolve funções que o
  React Compiler não consegue memoizar com segurança. Documentado no próprio
  arquivo.

## JavaScript

- Filtros combinados numa passada só, em vez de encadear `filter`/`map`
  (`js-combine-iterations`).
- `Map` dia → eventos na `WeekGrid`, em vez de varrer a lista sete vezes
  (`js-index-maps`).
- Máximo calculado em laço único no `MiniBarChart`, sem `sort` nem spread.

## Fontes e tema

- `next/font` para Inter, Playfair Display e JetBrains Mono: sem FOUT e sem
  requisição a terceiros em runtime.
- Script inline anti-flash no root layout aplica a classe `dark` antes da
  primeira pintura (`rendering-hydration-no-flicker`).

## Custo do carregamento percebido

Os mocks têm latência artificial (300–500ms) justamente para que skeletons,
`Suspense` e estados de carregamento sejam exercitados em desenvolvimento
como seriam em produção — e não apareçam quebrados só quando a API real
entrar.

## Não aplicado (com justificativa)

- **Virtualização de listas.** A regra vale para >50 itens; nenhuma lista
  atual chega perto. Revisitar na Etapa 1.2, quando publicações vierem
  paginadas do servidor.
- **`transition-all`** permanece nas primitivas geradas pelo shadcn/ui
  (`button`, `switch`, `tabs`, `progress`). É o baseline do fornecedor,
  isolado em `src/components/ui/**`; alterá-lo cria divergência a cada
  regeneração, com ganho desprezível.
- **`next/image`.** Ainda não há imagens de conteúdo — a marca é SVG inline.

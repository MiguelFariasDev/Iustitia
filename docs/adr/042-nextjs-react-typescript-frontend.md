# ADR-042: Next.js 16 + React 19 + TypeScript no frontend

**Status:** Aceito — implementado na Etapa 0.8

## Contexto

O produto precisa de uma interface web para escritórios de advocacia com
requisitos concretos: telas com muita leitura de dados (publicações, prazos,
agenda), navegação fluida entre módulos, SEO irrelevante nas rotas internas
mas relevante numa futura landing, e um time pequeno que não pode manter
duas stacks (uma para renderizar HTML, outra para a SPA).

O backend é .NET (ADR-001) e expõe uma API REST versionada (ADR-022/039). O
frontend é um cliente dessa API, não um monólito com acesso ao banco.

## Decisão

Next.js 16 (App Router) + React 19 + TypeScript `strict`, no diretório
`/web` (nome genérico — ADR-023).

Pontos que motivaram a escolha, em ordem de peso:

1. **Server Components + streaming.** As telas de dados fazem prefetch no
   servidor e entregam HTML já preenchido, com `Suspense` liberando o
   cabeçalho antes dos dados. O padrão está em todas as páginas de dados:
   `prefetchQuery` no servidor → `HydrationBoundary` → `useSuspenseQuery` no
   cliente, sem refetch na hidratação.
2. **App Router como convenção de arquivos.** `layout.tsx`, `loading.tsx`,
   `error.tsx`, `not-found.tsx` e route groups (`(auth)`, `(app)`) dão
   estrutura sem framework interno próprio.
3. **Proxy (ex-middleware) para roteamento por sessão.** `src/proxy.ts` faz
   o redirecionamento otimista de rotas protegidas. **Não é fronteira de
   segurança** — a API .NET valida o JWT em toda requisição (ADR-003/029).
4. **React Compiler** (`reactCompiler: true`): memoização automática, o que
   reduz `useMemo`/`useCallback` escritos à mão.
5. **`typedRoutes: true`**: rotas viram tipos; um link para uma rota
   inexistente quebra o build, não a navegação do usuário.

TypeScript em `strict`, sem `any` implícito. `next/font` para as três
famílias tipográficas (Inter, Playfair Display, JetBrains Mono), evitando
FOUT e requisições a terceiros em runtime.

## Consequências

**Positivas:**
- Uma stack só: o mesmo código busca dados no servidor e no cliente
  (`getDashboardSummary(client?)` recebe o cliente HTTP dos dois lados).
- `npm run build` faz typecheck junto; rotas inválidas falham cedo.
- O split Server/Client Component mantém o bundle do cliente pequeno: só
  vira `'use client'` o que realmente tem interatividade.

**Negativas / trade-offs:**
- A fronteira Server/Client é uma fonte recorrente de erro para quem não
  conhece o modelo (ex.: importar um store Zustand em Server Component).
  Mitigado por convenção: dados vêm por props/prefetch, estado de UI fica em
  componentes cliente.
- Next.js 16 é recente; algumas bibliotecas ainda assumem o middleware
  antigo. O React Compiler também não consegue memoizar a TanStack Table
  (`'use no memo'` em `DataTable`), um caso explicitamente documentado no
  código.
- Acoplamento a um framework com ciclo de release rápido. Aceito: o ganho de
  produtividade supera o custo de acompanhar as migrações, e a lógica de
  negócio está no backend, não aqui.

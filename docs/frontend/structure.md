# Estrutura do frontend

Aplicação Next.js 16 (App Router) em `/web`. O nome do diretório é genérico
de propósito — a marca aparece só na interface e em `NEXT_PUBLIC_APP_NAME`
(ADR-023).

## Árvore

```
web/
├── e2e/                      # Playwright: specs + helpers + auth.setup
├── src/
│   ├── app/                  # App Router
│   │   ├── (auth)/           # rotas públicas: login, convite, onboarding
│   │   ├── (app)/            # rotas autenticadas (AppLayout)
│   │   ├── layout.tsx        # root: fontes, providers, metadata
│   │   ├── globals.css       # tokens do design system
│   │   └── not-found.tsx / global-error.tsx
│   ├── components/
│   │   ├── ui/               # shadcn/ui (código copiado, estilo upstream)
│   │   ├── shared/           # componentes do produto, sem regra de negócio
│   │   └── layout/           # casca: Sidebar, Topbar, CommandPalette…
│   ├── features/             # uma pasta por domínio de tela
│   │   └── <feature>/
│   │       ├── types.ts      # contratos de dados
│   │       ├── mocks.ts      # dados de demonstração (Fase 0)
│   │       ├── api.ts        # acesso HTTP (serve servidor e cliente)
│   │       ├── queries.ts    # queryOptions do TanStack Query
│   │       ├── presentation.ts # rótulos, ícones e tons por enum
│   │       └── components/   # componentes da feature
│   ├── config/               # env, rotas, navegação, tokens de tema
│   ├── hooks/                # hooks reutilizáveis
│   ├── lib/                  # api, auth, query, supabase, format, notify
│   ├── providers/            # AppProviders, ThemeProvider, AuthListener
│   ├── stores/               # Zustand
│   ├── test/                 # harness do Vitest
│   └── proxy.ts              # roteamento otimista por sessão
```

## Regras de dependência

- `features/*` pode usar `components/*`, `lib/*`, `hooks/*`, `config/*`.
- `components/shared` **não** importa de `features/*` — se precisou, o
  componente pertence à feature.
- `components/ui` não importa nada do produto: são primitivas.
- `app/*` orquestra (prefetch, metadata, Suspense) e delega a renderização
  às features.

## Anatomia de uma rota com dados

Todas as telas de dados seguem a mesma forma. Exemplo em
`app/(app)/publicacoes/page.tsx`:

1. A página renderiza `PageHeader` e um `<Suspense>` com o skeleton — o
   cabeçalho é enviado imediatamente (streaming).
2. Dentro do Suspense, um Server Component assíncrono chama `connection()`
   (dados por tenant/requisição nunca são pré-renderizados em build), cria
   um `QueryClient` e faz `prefetchQuery`.
3. O resultado vai para o cliente num `HydrationBoundary`.
4. O componente cliente usa `useSuspenseQuery` com as **mesmas**
   `queryOptions` — o cache já está quente, não há refetch.
5. `QueryBoundary` (Suspense + ErrorBoundary) cobre erro e carregamento.

## Rotas

| Rota | Estado |
|---|---|
| `/painel` | Implementada (mocks) |
| `/publicacoes`, `/alertas`, `/agenda` | Implementadas (mocks) |
| `/configuracoes/*` | Implementadas (mocks) |
| `/processos`, `/contatos`, `/atendimentos`, `/financeiro`, `/criacao-de-pecas`, `/documentos`, `/indicadores` | Placeholder (`ComingSoon`) — Fases 2 a 5 |

As rotas de placeholder existem desde a Etapa 0.8 para que a navegação seja
completa e nenhum item da barra lateral leve a um 404.

## Mocks

Enquanto os endpoints não existem, `env.useMocks`
(`NEXT_PUBLIC_API_MOCKS`) faz cada `api.ts` devolver dados locais com
latência simulada — o que exercita skeletons e Suspense como em produção.
A assinatura `getX(client?)` é a mesma nos dois modos: quando a API existir,
muda só a flag.

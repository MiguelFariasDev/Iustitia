# ADR-044: TanStack Query para dados remotos, Zustand para estado de UI

**Status:** Aceito — implementado na Etapa 0.8

## Contexto

Há dois tipos de estado no frontend, com ciclos de vida diferentes:

1. **Estado do servidor**: publicações, alertas, agenda, usuários. Não nos
   pertence — é um cache local de algo que vive no backend. Precisa de
   revalidação, deduplicação, invalidação e tratamento de erro/carregamento.
2. **Estado de interface**: tema, barra lateral recolhida, paleta de
   comandos aberta, avisos de onboarding dispensados. É nosso, é síncrono e
   alguns pedaços precisam sobreviver ao reload.

Tratar os dois com a mesma ferramenta é o erro clássico: colocar resposta de
API num store global gera cache manual, dados obsoletos e invalidação
escrita à mão.

## Decisão

Ferramentas separadas, por tipo de estado.

**TanStack Query** para tudo que vem da API. Convenções adotadas:

- `queryKeys` hierárquicas (`src/lib/query/queryKeys.ts`) para permitir
  invalidação por prefixo.
- Um `queries.ts` por feature exportando `queryOptions(...)`, reaproveitado
  pelo prefetch no servidor e pelo `useSuspenseQuery` no cliente — a chave
  nunca é escrita duas vezes.
- Páginas de dados fazem `prefetchQuery` no servidor e entregam via
  `HydrationBoundary`; o cliente hidrata sem refetch.
- `useSuspenseQuery` sob um `QueryBoundary` (Suspense + ErrorBoundary com
  reset da query), para que carregamento e erro sejam declarativos.
- Exceção deliberada: contadores da navegação (`useNavCounts`) usam
  `useQuery`, não a versão suspense — a barra lateral não pode suspender o
  layout inteiro à espera de um número.

**Zustand** para o estado de interface, em stores pequenos e focados:
`authStore`, `uiStore`, `notificationStore`, `tenantStore`.

- `uiStore` persiste `theme`, `sidebarCollapsed` e `dismissedHints` no
  localStorage, com `skipHydration` e reidratação num efeito
  (`AppProviders`) — sem isso o HTML do servidor diverge do cliente.
- A flag `hydrated` existe por um motivo concreto: sem ela, um aviso de
  onboarding já dispensado pisca na tela antes de o localStorage ser lido.
- Componentes assinam **booleanos derivados**, não o objeto inteiro
  (`useUiStore((s) => s.hydrated && !s.dismissedHints.includes(id))`):
  dispensar um aviso não re-renderiza os outros.

Estado efêmero de uma tela (linha expandida, aba ativa, filtro) continua em
`useState` local — não precisa ser global nem persistido.

## Consequências

**Positivas:**
- Sem cache escrito à mão: revalidação, deduplicação e invalidação são do
  TanStack Query.
- As mesmas `queryOptions` servem servidor e cliente, eliminando a classe de
  bug "a chave do prefetch não bate com a do componente".
- Stores pequenos e testáveis: `uiStore`, `authStore`, `notificationStore` e
  `tenantStore` têm testes unitários próprios.

**Negativas / trade-offs:**
- Duas bibliotecas de estado exigem disciplina sobre onde cada coisa mora.
  A regra é simples e está no `docs/frontend/state-management.md`: veio da
  API → Query; é preferência de interface → Zustand; é efêmero da tela →
  `useState`.
- `skipHydration` + flag `hydrated` é cerimônia extra em todo consumidor de
  estado persistido, mas é o preço de não ter divergência de hidratação.
- Filtros e abas hoje vivem em `useState` e não no URL, então não são
  compartilháveis por link. Registrado como pendência para a Etapa 1.2,
  quando a filtragem passar a ser feita no servidor.

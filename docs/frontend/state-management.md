# Gerenciamento de estado

Decisão e justificativa em
[ADR-044](../adr/044-tanstack-query-zustand-estado.md).

## Onde cada coisa mora

| Tipo | Ferramenta | Exemplos |
|---|---|---|
| Veio da API | TanStack Query | publicações, alertas, agenda, usuários |
| Preferência de interface | Zustand | tema, sidebar recolhida, avisos dispensados |
| Efêmero de uma tela | `useState` | linha expandida, aba ativa, filtro atual |

Na dúvida: **se o backend é dono do dado, é Query.** Resposta de API nunca
vai para um store global.

## TanStack Query

### Query keys

Hierárquicas, em `src/lib/query/queryKeys.ts`, para permitir invalidação por
prefixo:

```ts
queryClient.invalidateQueries({ queryKey: queryKeys.publications.all })
```

### `queryOptions` compartilhadas

Cada feature exporta suas opções em `queries.ts`. O mesmo objeto é usado
pelo prefetch no servidor e pelo hook no cliente — a chave nunca é escrita
duas vezes, o que elimina a classe de bug "prefetch com chave diferente do
componente".

```ts
export const publicationsQuery = (client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.publications.list(),
    queryFn: () => getPublications(client),
  })
```

### Servidor → cliente

```tsx
const queryClient = makeQueryClient()
await queryClient.prefetchQuery(publicationsQuery(client))

<HydrationBoundary state={dehydrate(queryClient)}>
  <QueryBoundary fallback={<PublicationsSkeleton />} errorTitle="…">
    <PublicationsView />
  </QueryBoundary>
</HydrationBoundary>
```

No cliente, `useSuspenseQuery(publicationsQuery(apiClient))` encontra o cache
já quente.

### Suspense, e quando não usar

`useSuspenseQuery` é o padrão: carregamento e erro ficam declarativos sob o
`QueryBoundary`.

A exceção está em `useNavCounts`, que usa `useQuery`: a barra lateral é
visível em todas as telas e **não pode suspender o layout** à espera de um
contador. A `AgendaView` também usa `useQuery`, com
`placeholderData: (previous) => previous`, para manter a semana anterior na
tela durante a troca em vez de piscar uma grade vazia.

## Zustand

Quatro stores pequenos, em `src/stores/`:

| Store | Conteúdo | Persistido |
|---|---|---|
| `authStore` | usuário, sessão, `isAuthenticated` | não |
| `uiStore` | tema, sidebar, paleta, avisos dispensados | tema, sidebar, avisos |
| `notificationStore` | notificações in-app (sino) | não |
| `tenantStore` | escritório atual, feature flags | não |

### Persistência sem divergência de hidratação

`uiStore` usa `persist` com `skipHydration: true`. A reidratação acontece
num efeito, em `AppProviders`:

```ts
useEffect(() => {
  void useUiStore.persist.rehydrate()
}, [])
```

Sem isso, o HTML do servidor (que não tem localStorage) divergiria do
cliente. O `onRehydrateStorage` marca `hydrated: true`.

**A flag `hydrated` existe por um motivo concreto:** sem ela, um aviso de
onboarding já dispensado pisca na tela antes de o localStorage ser lido.
Por isso `useOnboardingHint` só considera o aviso visível depois da
reidratação.

### Assine o valor derivado, não o objeto

```ts
// Bom: só re-renderiza quando ESTE aviso muda.
const visible = useUiStore((s) => s.hydrated && !s.dismissedHints.includes(id))

// Ruim: re-renderiza a cada dispensa de qualquer aviso.
const { dismissedHints, hydrated } = useUiStore()
```

## Pendência conhecida

Filtros (publicações), aba ativa (alertas) e semana (agenda) vivem em
`useState` e **não** são refletidos no URL — logo, não são compartilháveis
por link nem sobrevivem a um reload.

As diretrizes de interface da Vercel recomendam sincronizar esse estado com
query params. A correção foi adiada deliberadamente para a Etapa 1.2, quando
a filtragem passará a ser feita no servidor e os parâmetros de URL serão
desenhados junto com o contrato da API — em vez de criar agora um formato
que teria de mudar em seguida.

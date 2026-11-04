import { dehydrate, HydrationBoundary } from '@tanstack/react-query'
import type { Metadata } from 'next'
import { connection } from 'next/server'
import { Suspense } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { env } from '@/config/env'
import { PublicacaoAtualizarButton } from '@/features/publicacoes/components/PublicacaoAtualizarButton'
import { PublicacoesSkeleton } from '@/features/publicacoes/components/PublicacoesSkeleton'
import { PublicacoesView } from '@/features/publicacoes/components/PublicacoesView'
import { filtersFromSearchParams } from '@/features/publicacoes/filters'
import { publicationsQuery, publicationsSummaryQuery } from '@/features/publicacoes/queries'
import { createServerApiClient } from '@/lib/api/server'
import { makeQueryClient } from '@/lib/query/queryClient'

export const metadata: Metadata = { title: 'Publicações' }

type SearchParams = Promise<Record<string, string | string[] | undefined>>

async function PublicacoesData({ searchParams }: { searchParams: SearchParams }) {
  // Dados por tenant/por requisição: nunca pré-renderizar em build.
  await connection()
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(await searchParams)) {
    if (typeof value === 'string') params.set(key, value)
  }

  const queryClient = makeQueryClient()
  // Lista e resumo em paralelo (sem waterfall). Falha no prefetch não derruba a página: o
  // cliente refaz a consulta e mostra o estado de erro no lugar certo.
  if (!env.useMocks) {
    const client = await createServerApiClient()
    await Promise.allSettled([
      queryClient.prefetchQuery(publicationsQuery(filtersFromSearchParams(params), client)),
      queryClient.prefetchQuery(publicationsSummaryQuery(client)),
    ])
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <PublicacoesView />
    </HydrationBoundary>
  )
}

export default function PublicacoesPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <>
      <PageHeader
        title="Publicações"
        description="Capturas dos diários oficiais para os processos cadastrados no escritório."
        actions={<PublicacaoAtualizarButton />}
      />
      <Suspense fallback={<PublicacoesSkeleton />}>
        <PublicacoesData searchParams={searchParams} />
      </Suspense>
    </>
  )
}

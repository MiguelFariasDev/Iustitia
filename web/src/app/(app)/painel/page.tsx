import { dehydrate, HydrationBoundary } from '@tanstack/react-query'
import type { Metadata } from 'next'
import { connection } from 'next/server'
import { Suspense } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { QueryBoundary } from '@/components/shared/QueryBoundary'
import { DashboardSkeleton } from '@/features/dashboard/components/DashboardSkeleton'
import { DashboardView } from '@/features/dashboard/components/DashboardView'
import { dashboardSummaryQuery } from '@/features/dashboard/queries'
import { createServerApiClient } from '@/lib/api/server'
import { makeQueryClient } from '@/lib/query/queryClient'

export const metadata: Metadata = { title: 'Painel de gestão' }

/**
 * Prefetch no servidor + HydrationBoundary: o HTML já chega com os dados e o cliente
 * hidrata o cache do TanStack Query sem novo fetch. O Suspense permite que o cabeçalho
 * seja enviado (streaming) enquanto os dados carregam.
 */
async function DashboardData() {
  // Dados por usuário/por requisição: nunca pré-renderizar em build.
  await connection()
  const queryClient = makeQueryClient()
  const client = await createServerApiClient()
  await queryClient.prefetchQuery(dashboardSummaryQuery(client))

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <QueryBoundary
        fallback={<DashboardSkeleton />}
        errorTitle="Não foi possível carregar o painel"
      >
        <DashboardView />
      </QueryBoundary>
    </HydrationBoundary>
  )
}

export default function DashboardPage() {
  return (
    <>
      <PageHeader
        title="Painel de gestão"
        description="Visão geral de prazos, publicações e agenda do escritório."
      />
      <Suspense fallback={<DashboardSkeleton />}>
        <DashboardData />
      </Suspense>
    </>
  )
}

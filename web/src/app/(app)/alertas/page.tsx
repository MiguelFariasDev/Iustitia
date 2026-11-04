import type { Metadata } from 'next'
import { Suspense } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Skeleton } from '@/components/ui/skeleton'
import { AlertasView } from '@/features/alertas/components/AlertasView'
import { MarcarTodosButton } from '@/features/alertas/components/MarcarTodosButton'

export const metadata: Metadata = { title: 'Alertas' }

export default function AlertasPage() {
  return (
    <>
      <PageHeader
        title="Alertas"
        description="Publicações novas e avisos que pedem a sua atenção."
        actions={<MarcarTodosButton />}
      />
      {/* useSearchParams (aba inicial vinda da URL) exige uma fronteira de Suspense. */}
      <Suspense fallback={<Skeleton className="h-96 rounded-xl" role="status" aria-label="Carregando alertas" />}>
        <AlertasView />
      </Suspense>
    </>
  )
}

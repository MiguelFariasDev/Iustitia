import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { IndicadoresView } from '@/features/indicadores/components/IndicadoresView'

export const metadata: Metadata = { title: 'Indicadores' }

export default function IndicadoresPage() {
  return (
    <>
      <PageHeader
        title="Indicadores"
        description="Visão analítica do escritório: carteira, publicações, prazos e produtividade."
      />
      <IndicadoresView />
    </>
  )
}

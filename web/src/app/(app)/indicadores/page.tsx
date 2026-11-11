import type { Metadata } from 'next'
import { ChartNoAxesCombined } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Indicadores' }

/** Sem backend ainda: a página não exibe dados de exemplo, só o que o módulo vai entregar. */
export default function Page() {
  return (
    <ComingSoon
      title="Indicadores"
      description="Visão analítica do escritório: carteira, publicações, prazos e produtividade."
      icon={ChartNoAxesCombined}
      phase="Fase 5"
      highlights={[
    'Carteira de processos por área e status',
    'Evolução das publicações recebidas',
    'Produtividade da equipe',
  ]}
    />
  )
}

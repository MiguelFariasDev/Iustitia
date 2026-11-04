import type { Metadata } from 'next'
import { ChartNoAxesCombined } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Indicadores' }

export default function IndicadoresPage() {
  return (
    <ComingSoon
      title="Indicadores"
      description="Painéis de produtividade, prazos e resultado financeiro."
      icon={ChartNoAxesCombined}
      phase="Fase 4"
      highlights={[
        'Produtividade por advogado e por equipe',
        'Cumprimento de prazos e gargalos de tratamento',
        'Receita, inadimplência e projeções',
      ]}
    />
  )
}

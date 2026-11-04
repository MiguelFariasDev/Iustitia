import type { Metadata } from 'next'
import { PenLine } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Criação de peças' }

export default function CriacaoDePecasPage() {
  return (
    <ComingSoon
      title="Criação de peças"
      description="Redação assistida por IA a partir dos dados do processo."
      icon={PenLine}
      phase="Fase 4"
      highlights={[
        'Modelos do escritório com variáveis preenchidas automaticamente',
        'Sugestões da Athena sempre sujeitas a revisão humana',
        'Versionamento e histórico de alterações de cada peça',
      ]}
    />
  )
}

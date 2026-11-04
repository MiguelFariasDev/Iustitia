import type { Metadata } from 'next'
import { Headset } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Atendimentos' }

export default function AtendimentosPage() {
  return (
    <ComingSoon
      title="Atendimentos"
      description="Registro de consultas, triagem e acompanhamento de clientes."
      icon={Headset}
      phase="Fase 4"
      highlights={[
        'Fila de atendimento com origem e responsável',
        'Histórico de interações por contato',
        'Conversão de atendimento em caso',
      ]}
    />
  )
}

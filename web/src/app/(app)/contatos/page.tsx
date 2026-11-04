import type { Metadata } from 'next'
import { Users } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Contatos' }

export default function ContatosPage() {
  return (
    <ComingSoon
      title="Contatos"
      description="Clientes, partes contrárias, peritos e demais envolvidos."
      icon={Users}
      phase="Fase 2"
      highlights={[
        'Pessoas físicas e jurídicas com histórico de relacionamento',
        'Vínculo com processos, atendimentos e cobranças',
        'Controle de conflito de interesses',
      ]}
    />
  )
}

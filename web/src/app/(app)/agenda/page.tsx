import type { Metadata } from 'next'
import { CalendarDays } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Agenda' }

export default function AgendaPage() {
  return (
    <ComingSoon
      title="Agenda"
      description="Audiências, reuniões, prazos e tarefas da equipe na semana."
      icon={CalendarDays}
      phase="Fase 4"
      highlights={[
        'Audiências e reuniões cadastradas pelo escritório',
        'Prazos processuais derivados das publicações capturadas',
        'Visão semanal por advogado responsável',
      ]}
    />
  )
}

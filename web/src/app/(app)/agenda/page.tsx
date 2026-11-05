import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { AgendaView } from '@/features/agenda/components/AgendaView'

export const metadata: Metadata = { title: 'Agenda' }

export default function AgendaPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Agenda" description="Audiências, reuniões, prazos e tarefas da equipe." />
      <AgendaView />
    </div>
  )
}

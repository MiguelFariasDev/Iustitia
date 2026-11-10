import type { Metadata } from 'next'
import { TarefasView } from '@/features/tarefas/components/TarefasView'

export const metadata: Metadata = { title: 'Tarefas' }

export default function TarefasPage() {
  return <TarefasView />
}

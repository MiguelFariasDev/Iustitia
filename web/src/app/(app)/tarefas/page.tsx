import type { Metadata } from 'next'
import { Suspense } from 'react'
import { TarefasView } from '@/features/tarefas/components/TarefasView'

export const metadata: Metadata = { title: 'Tarefas' }

export default function Page() {
  return (
    <Suspense>
      <TarefasView />
    </Suspense>
  )
}

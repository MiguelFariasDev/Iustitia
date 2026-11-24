import type { Metadata } from 'next'
import { PecasView } from '@/features/pecas/components/PecasView'

export const metadata: Metadata = { title: 'Criação de peças' }

export default function CriacaoDePecasPage() {
  return <PecasView />
}

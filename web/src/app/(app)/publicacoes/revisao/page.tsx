import type { Metadata } from 'next'
import { RevisaoView } from '@/features/revisao/components/RevisaoView'

export const metadata: Metadata = { title: 'Revisão da IA' }

export default function RevisaoPage() {
  return <RevisaoView />
}

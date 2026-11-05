import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import type { AtendimentoStatus } from '../types'

export const statusPresentation: Record<AtendimentoStatus, { label: string; tone: Tone }> = {
  Bot: { label: 'Bot', tone: 'neutral' },
  PendingHuman: { label: 'Aguardando', tone: 'warning' },
  Human: { label: 'Humano', tone: 'info' },
  Closed: { label: 'Encerrado', tone: 'success' },
}

/** A cor nunca é o único indicador: o texto do status sempre aparece (StatusBadge). */
export function AtendimentoStatusBadge({ status }: { status: AtendimentoStatus }) {
  const { label, tone } = statusPresentation[status]
  return <StatusBadge tone={tone}>{label}</StatusBadge>
}

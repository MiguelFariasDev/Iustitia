import {
  CalendarClock,
  FileText,
  Gavel,
  Mail,
  Scale,
  ScrollText,
  type LucideIcon,
} from 'lucide-react'
import type { Tone } from '@/components/shared/tones'
import type { PublicationKind, PublicationStatus } from './types'

export const kindMeta: Record<PublicationKind, { label: string; icon: LucideIcon }> = {
  intimation: { label: 'Intimação', icon: Mail },
  citation: { label: 'Citação', icon: ScrollText },
  decision: { label: 'Decisão', icon: Scale },
  sentence: { label: 'Sentença', icon: Gavel },
  hearing: { label: 'Audiência', icon: CalendarClock },
  other: { label: 'Outros', icon: FileText },
}

export const statusMeta: Record<PublicationStatus, { label: string; tone: Tone }> = {
  untreated: { label: 'Não tratada', tone: 'warning' },
  treated: { label: 'Tratada', tone: 'success' },
  discarded: { label: 'Descartada', tone: 'neutral' },
  archived: { label: 'Arquivada', tone: 'info' },
}

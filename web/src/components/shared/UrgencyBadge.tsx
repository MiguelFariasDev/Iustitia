import {
  AlertOctagon,
  CalendarClock,
  CalendarDays,
  Clock,
  FileSearch,
  Sparkles,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'

export type UrgencyLevel = 'overdue' | 'today' | 'tomorrow' | 'week' | 'review' | 'pending'

const meta: Record<UrgencyLevel, { label: string; icon: LucideIcon; classes: string }> = {
  overdue: {
    label: 'Atrasado',
    icon: AlertOctagon,
    classes: 'border-destructive/30 bg-destructive-soft text-destructive-foreground',
  },
  today: {
    label: 'Vence hoje',
    icon: Clock,
    classes: 'border-warning/30 bg-warning/10 text-warning-foreground',
  },
  tomorrow: {
    label: 'Amanhã',
    icon: CalendarClock,
    classes: 'border-border bg-muted text-muted-foreground',
  },
  week: {
    label: 'Esta semana',
    icon: CalendarDays,
    classes: 'border-border bg-muted text-muted-foreground',
  },
  // Pendência de revisão humana SEM IA envolvida: mesmo assunto, sem o violeta nem o ✨.
  pending: {
    label: 'Para revisar',
    icon: FileSearch,
    classes: 'border-border bg-muted text-muted-foreground',
  },
  review: {
    label: 'Para revisar',
    icon: Sparkles,
    classes: 'border-ia/30 bg-ia-soft text-ia-text',
  },
}

/** Urgência com ÍCONE e TEXTO: a cor sozinha nunca diferencia (acessibilidade). */
export function UrgencyBadge({
  level,
  label: labelOverride,
  className,
}: {
  level: UrgencyLevel
  /** Troca o texto padrão (ex.: "Hoje" para uma reunião, que não "vence"). */
  label?: string
  className?: string
}) {
  const { label: padrao, icon: Icon, classes } = meta[level]
  const label = labelOverride ?? padrao

  return (
    <span
      data-urgency={level}
      className={cn(
        'inline-flex items-center gap-1 rounded-xs border px-2 py-0.5 text-xs font-medium whitespace-nowrap',
        classes,
        className
      )}
    >
      <Icon className="size-3 shrink-0" aria-hidden />
      {label}
    </span>
  )
}

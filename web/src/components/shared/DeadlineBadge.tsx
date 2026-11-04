import { AlarmClock, AlertOctagon, CalendarClock } from 'lucide-react'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { getDeadlineLabel, getDeadlineUrgency, urgencyTone } from './deadline'
import { toneClasses } from './tones'

interface DeadlineBadgeProps {
  dueDate: string | Date
  /** Injetável para testes e para renderização consistente servidor/cliente. */
  now?: Date
  className?: string
}

export function DeadlineBadge({ dueDate, now, className }: DeadlineBadgeProps) {
  const due = new Date(dueDate)
  const urgency = getDeadlineUrgency(due, now)
  const Icon =
    urgency === 'overdue' || urgency === 'today'
      ? AlertOctagon
      : urgency === 'ok'
        ? CalendarClock
        : AlarmClock

  return (
    <span
      data-urgency={urgency}
      title={`Prazo: ${formatDate(due)}`}
      className={cn(
        'inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-xs font-medium whitespace-nowrap',
        toneClasses[urgencyTone[urgency]].badge,
        urgency === 'overdue' && 'font-semibold',
        className
      )}
    >
      <Icon className="size-3" aria-hidden />
      {getDeadlineLabel(due, now)}
    </span>
  )
}

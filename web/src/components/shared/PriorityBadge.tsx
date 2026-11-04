import { ArrowDown, ArrowUp, ChevronsUp, Minus, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { toneClasses, type Tone } from './tones'

export type Priority = 'low' | 'medium' | 'high' | 'urgent'

const config: Record<Priority, { label: string; tone: Tone; icon: LucideIcon }> = {
  low: { label: 'Baixa', tone: 'neutral', icon: ArrowDown },
  medium: { label: 'Média', tone: 'info', icon: Minus },
  high: { label: 'Alta', tone: 'warning', icon: ArrowUp },
  urgent: { label: 'Urgente', tone: 'danger', icon: ChevronsUp },
}

export function PriorityBadge({ priority, className }: { priority: Priority; className?: string }) {
  const { label, tone, icon: Icon } = config[priority]
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-xs font-medium',
        toneClasses[tone].badge,
        className
      )}
    >
      <Icon className="size-3" aria-hidden />
      <span>
        <span className="sr-only">Prioridade </span>
        {label}
      </span>
    </span>
  )
}

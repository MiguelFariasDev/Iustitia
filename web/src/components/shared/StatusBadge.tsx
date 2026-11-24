import { cn } from '@/lib/utils'
import { toneClasses, type Tone } from './tones'

interface StatusBadgeProps {
  tone?: Tone
  children: React.ReactNode
  /** Oculta o ponto colorido (a cor nunca é o único indicador: o texto sempre existe). */
  hideDot?: boolean
  className?: string
}

export function StatusBadge({ tone = 'neutral', children, hideDot, className }: StatusBadgeProps) {
  const classes = toneClasses[tone]
  return (
    <span
      className={cn(
        'iu-label inline-flex h-6 items-center gap-1.5 rounded-sm border px-2 whitespace-nowrap',
        classes.badge,
        className
      )}
    >
      {hideDot ? null : <span className={cn('size-1.5 rounded-full', classes.dot)} aria-hidden />}
      {children}
    </span>
  )
}

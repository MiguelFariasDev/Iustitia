import { cn } from '@/lib/utils'

interface StepProgressProps {
  steps: readonly string[]
  /** Índice 0-based da etapa atual. */
  current: number
  className?: string
}

/** Indicador `01 Você · 02 Escritório` em mono, com barra que preenche por `scaleX` (.25s). */
export function StepProgress({ steps, current, className }: StepProgressProps) {
  const fraction = (current + 1) / steps.length

  return (
    <div className={cn('space-y-2', className)}>
      <ol className="iu-mono flex gap-4 text-xs">
        {steps.map((label, index) => (
          <li
            key={label}
            aria-current={index === current ? 'step' : undefined}
            className={index === current ? 'font-semibold text-foreground' : 'text-muted-foreground'}
          >
            {String(index + 1).padStart(2, '0')} {label}
          </li>
        ))}
      </ol>
      <div
        role="progressbar"
        aria-label="Progresso"
        aria-valuemin={1}
        aria-valuemax={steps.length}
        aria-valuenow={current + 1}
        aria-valuetext={`Etapa ${current + 1} de ${steps.length}: ${steps[current]}`}
        className="h-1 overflow-hidden rounded-full bg-border"
      >
        <div
          className="h-full origin-left rounded-full bg-[var(--iu-brand)] transition-transform duration-[250ms] ease-out"
          style={{ transform: `scaleX(${fraction})` }}
        />
      </div>
    </div>
  )
}

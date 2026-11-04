import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'

export function OnboardingStepper({ steps, current }: { steps: string[]; current: number }) {
  return (
    <ol className="flex items-center gap-2" aria-label="Etapas do cadastro">
      {steps.map((label, index) => {
        const done = index < current
        const active = index === current
        return (
          <li
            key={label}
            aria-current={active ? 'step' : undefined}
            className="flex flex-1 items-center gap-2"
          >
            <span
              className={cn(
                'flex size-7 shrink-0 items-center justify-center rounded-full border text-xs font-semibold',
                done && 'border-secondary bg-secondary text-secondary-foreground',
                active && 'border-secondary text-secondary',
                !done && !active && 'text-muted-foreground'
              )}
            >
              {done ? <Check className="size-3.5" aria-hidden /> : index + 1}
            </span>
            <span
              className={cn(
                'hidden text-sm sm:inline',
                active ? 'font-medium' : 'text-muted-foreground'
              )}
            >
              {label}
              {done ? <span className="sr-only"> (concluída)</span> : null}
            </span>
            {index < steps.length - 1 ? (
              <span className="h-px flex-1 bg-border" aria-hidden />
            ) : null}
          </li>
        )
      })}
    </ol>
  )
}

import { Check, X } from 'lucide-react'
import { cn } from '@/lib/utils'

const rules = [
  { label: 'Pelo menos 8 caracteres', test: (v: string) => v.length >= 8 },
  { label: 'Uma letra minúscula', test: (v: string) => /[a-z]/.test(v) },
  { label: 'Uma letra maiúscula', test: (v: string) => /[A-Z]/.test(v) },
  { label: 'Um número', test: (v: string) => /\d/.test(v) },
]

export function PasswordRequirements({ value, id }: { value: string; id?: string }) {
  return (
    <ul
      id={id}
      className="grid grid-cols-1 gap-1 text-xs sm:grid-cols-2"
      aria-label="Requisitos da senha"
    >
      {rules.map((rule) => {
        const ok = rule.test(value)
        return (
          <li
            key={rule.label}
            className={cn(
              'flex items-center gap-1.5',
              ok ? 'text-success-foreground' : 'text-muted-foreground'
            )}
          >
            {ok ? (
              <Check className="size-3.5" aria-hidden />
            ) : (
              <X className="size-3.5" aria-hidden />
            )}
            {rule.label}
            <span className="sr-only">{ok ? ' (atendido)' : ' (pendente)'}</span>
          </li>
        )
      })}
    </ul>
  )
}

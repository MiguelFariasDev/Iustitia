import { cn } from '@/lib/utils'

interface FilterChipProps {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}

/**
 * Chip de filtro alternável. `aria-pressed` comunica o estado a leitores de tela; a cor sozinha
 * nunca diferencia ativo de inativo (o preenchimento muda, e o estado também é falado).
 */
export function FilterChip({ active, onClick, children }: FilterChipProps) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        'h-8 rounded-full border px-3 text-[13px] font-medium transition-colors',
        active
          ? 'border-primary bg-brand-soft font-semibold text-primary'
          : 'border-input bg-card text-muted-foreground hover:bg-accent hover:text-foreground'
      )}
    >
      {children}
    </button>
  )
}

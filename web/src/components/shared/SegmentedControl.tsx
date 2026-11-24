'use client'

import { cn } from '@/lib/utils'

interface SegmentedControlProps<T extends string> {
  /** `null` = o estado atual não é nenhuma das opções (ex.: filtro combinado): nada marcado. */
  value: T | null
  onChange: (value: T) => void
  options: readonly { value: T; label: string }[]
  /** Nome acessível do grupo (ex.: "Filtrar a fila"). */
  label: string
  className?: string
}

/**
 * Escolha de UMA opção entre poucas (variação A/B, filtros de lista). É `radiogroup`: leitor de
 * tela anuncia "N de M" e o estado marcado, e as setas movem a seleção.
 */
export function SegmentedControl<T extends string>({
  value,
  onChange,
  options,
  label,
  className,
}: SegmentedControlProps<T>) {
  function onKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    const passo =
      event.key === 'ArrowRight' || event.key === 'ArrowDown'
        ? 1
        : event.key === 'ArrowLeft' || event.key === 'ArrowUp'
          ? -1
          : 0
    if (passo === 0) return
    event.preventDefault()
    const atual = options.findIndex((opcao) => opcao.value === value)
    // Sem opção marcada, a seta para a frente vai à primeira e a de volta, à última.
    const base = atual === -1 ? (passo === 1 ? -1 : 0) : atual
    const proximo = options[(base + passo + options.length) % options.length]
    if (proximo) onChange(proximo.value)
  }

  return (
    <div
      role="radiogroup"
      aria-label={label}
      onKeyDown={onKeyDown}
      className={cn(
        'inline-flex max-w-full gap-0.5 overflow-x-auto rounded-sm border bg-muted p-0.5',
        className
      )}
    >
      {options.map((opcao, indice) => {
        const ativo = opcao.value === value
        // Roving tabindex: a marcada entra na ordem de Tab; sem marcada, a primeira.
        const entraNoTab = ativo || (value === null && indice === 0)

        return (
          <button
            key={opcao.value}
            type="button"
            role="radio"
            aria-checked={ativo}
            tabIndex={entraNoTab ? 0 : -1}
            onClick={() => onChange(opcao.value)}
            className={cn(
              'shrink-0 rounded-xs px-3 py-1.5 text-sm font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring',
              ativo
                ? 'bg-card text-foreground shadow-xs'
                : 'text-muted-foreground hover:bg-accent hover:text-foreground'
            )}
          >
            {opcao.label}
          </button>
        )
      })}
    </div>
  )
}

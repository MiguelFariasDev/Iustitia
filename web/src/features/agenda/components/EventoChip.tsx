'use client'

import { cn } from '@/lib/utils'
import { formatHora } from '../dates'
import { tipoEventoClasses, tipoEventoLabels, type AgendaEvento } from '../types'

interface EventoChipProps {
  evento: AgendaEvento
  onSelect: (evento: AgendaEvento) => void
  /** Mostra a hora antes do título (omitida em evento de dia inteiro). */
  showTime?: boolean
}

/**
 * Chip de um evento. É um <button>: alcançável por teclado, e Enter/Espaço abre a edição —
 * que é também o caminho acessível para remarcar, já que arrastar exige ponteiro.
 *
 * O tipo aparece por TEXTO no rótulo acessível além da cor.
 */
export function EventoChip({ evento, onSelect, showTime = true }: EventoChipProps) {
  const hora = showTime && !evento.diaInteiro ? `${formatHora(evento.inicio)} ` : ''

  return (
    <button
      type="button"
      draggable
      onDragStart={(event) => {
        event.dataTransfer.setData('text/plain', evento.id)
        event.dataTransfer.effectAllowed = 'move'
      }}
      onClick={() => onSelect(evento)}
      aria-label={`${tipoEventoLabels[evento.tipo]}: ${evento.titulo}${hora ? `, às ${formatHora(evento.inicio)}` : ''}`}
      className={cn(
        'w-full cursor-grab truncate rounded border px-1.5 py-0.5 text-left text-xs font-medium',
        'focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none active:cursor-grabbing',
        tipoEventoClasses[evento.tipo]
      )}
    >
      {hora}
      {evento.titulo}
    </button>
  )
}

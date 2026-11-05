'use client'

import { cn } from '@/lib/utils'
import { diasSemanaCurtos, eventosDoDia, isSameDay, monthGrid, moveToDay } from '../dates'
import type { AgendaEvento } from '../types'
import { EventoChip } from './EventoChip'

interface MonthGridProps {
  anchor: Date
  eventos: readonly AgendaEvento[]
  onSelectEvento: (evento: AgendaEvento) => void
  onCreateAt: (day: Date) => void
  onMove: (evento: AgendaEvento, periodo: { inicio: string; fim: string }) => void
}

const MAX_VISIVEIS = 3

export function MonthGrid({ anchor, eventos, onSelectEvento, onCreateAt, onMove }: MonthGridProps) {
  const weeks = monthGrid(anchor)
  const today = new Date()

  return (
    <div role="grid" aria-label="Calendário do mês" className="overflow-hidden rounded-lg border">
      <div role="row" className="grid grid-cols-7 border-b bg-muted/50 text-xs font-medium">
        {diasSemanaCurtos.map((dia) => (
          <div key={dia} role="columnheader" className="px-2 py-1.5 text-center uppercase">
            {dia}
          </div>
        ))}
      </div>

      {weeks.map((week) => (
        <div
          key={week[0]!.toISOString()}
          role="row"
          className="grid grid-cols-7 border-b last:border-b-0"
        >
          {week.map((day) => {
            const doDia = eventosDoDia(eventos, day)
            const foraDoMes = day.getMonth() !== anchor.getMonth()

            return (
              <div
                key={day.toISOString()}
                role="gridcell"
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => {
                  event.preventDefault()
                  const evento = eventos.find(
                    (e) => e.id === event.dataTransfer.getData('text/plain')
                  )
                  if (evento) onMove(evento, moveToDay(evento, day))
                }}
                className={cn(
                  'min-h-24 space-y-1 border-r p-1 last:border-r-0',
                  foraDoMes && 'bg-muted/30'
                )}
              >
                <button
                  type="button"
                  onClick={() => onCreateAt(day)}
                  aria-label={`Novo evento em ${day.toLocaleDateString('pt-BR')}`}
                  className={cn(
                    'flex size-6 items-center justify-center rounded-full text-xs focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                    foraDoMes && 'text-muted-foreground',
                    isSameDay(day, today) && 'bg-primary font-semibold text-primary-foreground'
                  )}
                >
                  {day.getDate()}
                </button>

                {doDia.slice(0, MAX_VISIVEIS).map((evento) => (
                  <EventoChip key={evento.id} evento={evento} onSelect={onSelectEvento} />
                ))}
                {doDia.length > MAX_VISIVEIS ? (
                  <p className="px-1 text-xs text-muted-foreground">
                    +{doDia.length - MAX_VISIVEIS} mais
                  </p>
                ) : null}
              </div>
            )
          })}
        </div>
      ))}
    </div>
  )
}

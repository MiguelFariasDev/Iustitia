'use client'

import { cn } from '@/lib/utils'
import { diasSemanaCurtos, eventosDoDia, isSameDay, moveToSlot } from '../dates'
import type { AgendaEvento } from '../types'
import { EventoChip } from './EventoChip'

interface TimeGridProps {
  /** 7 dias na visão semanal, 1 na diária. */
  days: readonly Date[]
  eventos: readonly AgendaEvento[]
  onSelectEvento: (evento: AgendaEvento) => void
  onCreateAt: (day: Date, hour: number) => void
  onMove: (evento: AgendaEvento, periodo: { inicio: string; fim: string }) => void
}

const HORA_INICIAL = 7
const HORA_FINAL = 20
const horas = Array.from({ length: HORA_FINAL - HORA_INICIAL + 1 }, (_, i) => HORA_INICIAL + i)

/**
 * Grade por hora. Um evento aparece na linha da hora em que COMEÇA (a altura não representa a
 * duração) — escolha deliberada para manter o arrastar e o foco simples; o horário completo
 * está no rótulo do chip. Eventos fora de 07h–20h entram na faixa mais próxima.
 */
export function TimeGrid({ days, eventos, onSelectEvento, onCreateAt, onMove }: TimeGridProps) {
  const today = new Date()
  const colunas = { gridTemplateColumns: `3.5rem repeat(${days.length}, minmax(0, 1fr))` }

  const naHora = (day: Date, hora: number) =>
    eventosDoDia(eventos, day).filter((e) => {
      if (e.diaInteiro) return false
      const h = Math.min(HORA_FINAL, Math.max(HORA_INICIAL, new Date(e.inicio).getHours()))
      return h === hora
    })

  return (
    <div
      role="grid"
      aria-label="Agenda por horário"
      className="animate-iu-in overflow-hidden rounded-lg border"
    >
      <div role="row" style={colunas} className="grid border-b bg-muted/50 text-xs font-medium">
        <div role="columnheader" className="px-2 py-1.5" />
        {days.map((day) => (
          <div
            key={day.toISOString()}
            role="columnheader"
            className={cn('px-2 py-1.5 text-center', isSameDay(day, today) && 'text-primary')}
          >
            {diasSemanaCurtos[(day.getDay() + 6) % 7]} {day.getDate()}
          </div>
        ))}
      </div>

      <div role="row" style={colunas} className="grid border-b">
        <div role="rowheader" className="px-2 py-1 text-xs text-muted-foreground">
          Dia todo
        </div>
        {days.map((day) => (
          <div key={day.toISOString()} role="gridcell" className="space-y-1 border-l p-1">
            {eventosDoDia(eventos, day)
              .filter((e) => e.diaInteiro)
              .map((evento) => (
                <EventoChip
                  key={evento.id}
                  evento={evento}
                  onSelect={onSelectEvento}
                  showTime={false}
                />
              ))}
          </div>
        ))}
      </div>

      {horas.map((hora) => (
        <div key={hora} role="row" style={colunas} className="grid border-b last:border-b-0">
          <div role="rowheader" className="px-2 py-1 text-xs text-muted-foreground">
            {String(hora).padStart(2, '0')}:00
          </div>
          {days.map((day) => (
            <div
              key={day.toISOString()}
              role="gridcell"
              onDragOver={(event) => event.preventDefault()}
              onDrop={(event) => {
                event.preventDefault()
                const evento = eventos.find(
                  (e) => e.id === event.dataTransfer.getData('text/plain')
                )
                if (evento) onMove(evento, moveToSlot(evento, day, hora))
              }}
              onDoubleClick={() => onCreateAt(day, hora)}
              className={cn(
                'min-h-10 space-y-1 border-l p-1',
                isSameDay(day, today) && 'bg-brand-soft'
              )}
            >
              {naHora(day, hora).map((evento) => (
                <EventoChip key={evento.id} evento={evento} onSelect={onSelectEvento} />
              ))}
            </div>
          ))}
        </div>
      ))}
    </div>
  )
}

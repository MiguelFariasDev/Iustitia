'use client'

import { ChevronLeft, ChevronRight, Plus } from 'lucide-react'
import { useState } from 'react'
import { ErrorState } from '@/components/shared/ErrorState'
import { FilterChip } from '@/components/shared/FilterChip'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { periodFor, shiftAnchor, titleFor, weekDays } from '../dates'
import { useAgenda, useRescheduleEvento } from '../hooks'
import {
  tipoEventoClasses,
  tipoEventoLabels,
  tiposEvento,
  visaoAgendaLabels,
  visoesAgenda,
  type AgendaEvento,
  type TipoEvento,
  type VisaoAgenda,
} from '../types'
import { cn } from '@/lib/utils'
import { EventoDialog, type EventoDialogState } from './EventoDialog'
import { MonthGrid } from './MonthGrid'
import { TimeGrid } from './TimeGrid'

export function AgendaView() {
  const [view, setView] = useState<VisaoAgenda>('mes')
  const [anchor, setAnchor] = useState(() => new Date())
  const [tipos, setTipos] = useState<ReadonlySet<TipoEvento>>(new Set())
  const [dialog, setDialog] = useState<EventoDialogState | null>(null)

  const periodo = periodFor(view, anchor)
  const { data, error, isPending, isError, refetch } = useAgenda(periodo)
  const reschedule = useRescheduleEvento()

  const eventos = (data ?? []).filter((e) => tipos.size === 0 || tipos.has(e.tipo))

  function toggleTipo(tipo: TipoEvento) {
    setTipos((atual) => {
      const next = new Set(atual)
      if (next.has(tipo)) next.delete(tipo)
      else next.add(tipo)

      return next
    })
  }

  function move(evento: AgendaEvento, novo: { inicio: string; fim: string }) {
    if (novo.inicio === evento.inicio) return
    reschedule.mutate({ id: evento.id, ...novo })
  }

  const edit = (evento: AgendaEvento) => setDialog({ evento })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon"
            aria-label="Período anterior"
            onClick={() => setAnchor(shiftAnchor(view, anchor, -1))}
          >
            <ChevronLeft aria-hidden />
          </Button>
          <Button variant="outline" size="sm" onClick={() => setAnchor(new Date())}>
            Hoje
          </Button>
          <Button
            variant="outline"
            size="icon"
            aria-label="Próximo período"
            onClick={() => setAnchor(shiftAnchor(view, anchor, 1))}
          >
            <ChevronRight aria-hidden />
          </Button>
        </div>

        <h2 aria-live="polite" className="text-lg font-semibold">
          {titleFor(view, anchor)}
        </h2>

        <div className="ml-auto flex flex-wrap items-center gap-2">
          <div role="group" aria-label="Visão da agenda" className="flex gap-1">
            {visoesAgenda.map((valor) => (
              <FilterChip key={valor} active={view === valor} onClick={() => setView(valor)}>
                {visaoAgendaLabels[valor]}
              </FilterChip>
            ))}
          </div>
          <Button size="sm" onClick={() => setDialog({})}>
            <Plus aria-hidden />
            Novo evento
          </Button>
        </div>
      </div>

      <div role="group" aria-label="Filtrar por tipo" className="flex flex-wrap items-center gap-2">
        {tiposEvento.map((tipo) => (
          <FilterChip key={tipo} active={tipos.has(tipo)} onClick={() => toggleTipo(tipo)}>
            <span className="flex items-center gap-1.5">
              <span
                aria-hidden
                className={cn('size-2 rounded-full border', tipoEventoClasses[tipo])}
              />
              {tipoEventoLabels[tipo]}
            </span>
          </FilterChip>
        ))}
      </div>

      {isError ? (
        <ErrorState
          error={error}
          title="Não foi possível carregar a agenda."
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <Skeleton className="h-[32rem] w-full" />
      ) : view === 'mes' ? (
        <MonthGrid
          anchor={anchor}
          eventos={eventos}
          onSelectEvento={edit}
          onCreateAt={(day) => setDialog({ inicio: withHour(day, 9) })}
          onMove={move}
        />
      ) : (
        <TimeGrid
          days={view === 'dia' ? [anchor] : weekDays(anchor)}
          eventos={eventos}
          onSelectEvento={edit}
          onCreateAt={(day, hour) => setDialog({ inicio: withHour(day, hour) })}
          onMove={move}
        />
      )}

      <p className="text-xs text-muted-foreground">
        Arraste um evento para remarcar. Pelo teclado, abra o evento e altere a data. Na semana e no
        dia, dê dois cliques num horário para criar.
      </p>

      <EventoDialog state={dialog} onClose={() => setDialog(null)} />
    </div>
  )
}

function withHour(day: Date, hour: number): Date {
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), hour, 0)
}

'use client'

import { useQuery } from '@tanstack/react-query'
import { CalendarClock, CalendarPlus, Gavel } from 'lucide-react'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { Button } from '@/components/ui/button'
import { EventoDialog, type EventoDialogState } from '@/features/agenda/components/EventoDialog'
import { formatHora } from '@/features/agenda/dates'
import { eventosDoProcessoQuery } from '@/features/agenda/queries'
import { tipoEventoLabels, type AgendaEvento, type TipoEvento } from '@/features/agenda/types'
import { useNow } from '@/hooks/useNow'
import { formatDate } from '@/lib/format'
import { presetDoEvento } from './NovoNoProcessoMenu'

const tomDoTipo: Record<TipoEvento, Tone> = {
  Prazo: 'danger',
  Audiencia: 'warning',
  Reuniao: 'info',
  Tarefa: 'neutral',
}

/** Grupos da aba: prazos e audiências à parte; reuniões, atendimentos e o resto em "Eventos". */
const grupos: readonly { titulo: string; tipos: readonly TipoEvento[] }[] = [
  { titulo: 'Prazos', tipos: ['Prazo'] },
  { titulo: 'Audiências', tipos: ['Audiencia'] },
  { titulo: 'Eventos e atendimentos', tipos: ['Reuniao', 'Tarefa'] },
]

/**
 * Prazos, audiências e eventos ligados ao processo (vêm da agenda). Clicar num item abre a edição;
 * os botões criam já vinculado ao processo.
 */
export function CompromissosDoProcesso({
  processoId,
  clienteNome,
}: {
  processoId: string
  clienteNome: string | null
}) {
  const { data, isPending, isError, refetch } = useQuery(eventosDoProcessoQuery(processoId))
  const [dialogo, setDialogo] = useState<EventoDialogState | null>(null)
  const agora = useNow()

  const botoes = (
    <div className="flex flex-wrap gap-2">
      <Button size="sm" variant="outline" onClick={() => setDialogo(presetDoEvento('prazo', processoId, clienteNome))}>
        <CalendarClock aria-hidden />
        Novo prazo
      </Button>
      <Button size="sm" variant="outline" onClick={() => setDialogo(presetDoEvento('audiencia', processoId, clienteNome))}>
        <Gavel aria-hidden />
        Nova audiência
      </Button>
      <Button size="sm" variant="outline" onClick={() => setDialogo(presetDoEvento('evento', processoId, clienteNome))}>
        <CalendarPlus aria-hidden />
        Novo evento
      </Button>
    </div>
  )

  return (
    <div className="space-y-4">
      {botoes}
      <EventoDialog state={dialogo} onClose={() => setDialogo(null)} />

      {isPending ? (
        <LoadingSpinner label="Carregando a agenda do processo…" />
      ) : isError ? (
        <p role="alert" className="text-sm text-destructive">
          Não foi possível carregar a agenda do processo.{' '}
          <button type="button" className="underline underline-offset-4" onClick={() => void refetch()}>
            Tentar de novo
          </button>
        </p>
      ) : data.length === 0 ? (
        <EmptyState
          icon={CalendarClock}
          title="Nenhum prazo, audiência ou evento neste processo"
          description="Use os botões acima: o que for criado aqui aparece também na agenda do escritório."
        />
      ) : (
        <div className="space-y-6">
          {grupos.map((grupo) => {
            const itens = data.filter((evento) => grupo.tipos.includes(evento.tipo))
            if (itens.length === 0) return null

            return (
              <section key={grupo.titulo} aria-label={grupo.titulo} className="space-y-2">
                <h3 className="text-sm font-semibold">
                  {grupo.titulo} <span className="font-mono text-xs text-muted-foreground">{itens.length}</span>
                </h3>
                <ul className="divide-y rounded-lg border">
                  {itens.map((evento) => (
                    <ItemDaAgenda
                      key={evento.id}
                      evento={evento}
                      passou={agora !== null && new Date(evento.fim) < agora}
                      onAbrir={() => setDialogo({ evento })}
                    />
                  ))}
                </ul>
              </section>
            )
          })}
        </div>
      )}
    </div>
  )
}

function ItemDaAgenda({
  evento,
  passou,
  onAbrir,
}: {
  evento: AgendaEvento
  passou: boolean
  onAbrir: () => void
}) {
  return (
    <li>
      <button
        type="button"
        onClick={onAbrir}
        className="flex w-full flex-wrap items-center gap-3 px-4 py-3 text-left outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
        aria-label={`Abrir ${tipoEventoLabels[evento.tipo]}: ${evento.titulo}`}
      >
        <StatusBadge tone={passou ? 'neutral' : tomDoTipo[evento.tipo]}>
          {tipoEventoLabels[evento.tipo]}
        </StatusBadge>
        <span className={passou ? 'min-w-0 flex-1 truncate text-sm text-muted-foreground' : 'min-w-0 flex-1 truncate text-sm font-medium'}>
          {evento.titulo}
        </span>
        {evento.local ? <span className="text-xs text-muted-foreground">{evento.local}</span> : null}
        <time dateTime={evento.inicio} className="font-mono text-xs text-muted-foreground">
          {formatDate(evento.inicio)}
          {evento.diaInteiro ? '' : ` ${formatHora(evento.inicio)}`}
        </time>
        {passou ? <span className="text-xs text-muted-foreground">realizado</span> : null}
      </button>
    </li>
  )
}

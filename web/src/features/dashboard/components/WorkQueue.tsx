'use client'

import { CalendarCheck, Sparkles } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { UrgencyBadge } from '@/components/shared/UrgencyBadge'
import { Skeleton } from '@/components/ui/skeleton'
import { formatHora } from '@/features/agenda/dates'
import { cn } from '@/lib/utils'
import { filterQueue, type QueueFilter, type QueueItem } from '../workQueue'

const filtros: { value: QueueFilter; label: string }[] = [
  { value: 'all', label: 'Todos' },
  { value: 'today', label: 'Hoje e atrasados' },
  { value: 'ai', label: 'Sugeridos pela IA' },
]

const dataCurta = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' })

function prazoTexto(item: QueueItem) {
  if (!item.due) return '—'
  if (item.urgency === 'today') return formatHora(item.due)

  return dataCurta.format(new Date(item.due))
}

interface WorkQueueProps {
  items: readonly QueueItem[]
  isPending: boolean
  isError: boolean
  onRetry: () => void
  className?: string
}

/**
 * Fila de trabalho priorizada. Cada linha é um link (o título leva ao compromisso ou à revisão);
 * a urgência tem ícone e texto, nunca só cor. O selo ✨ marca o que veio da IA.
 */
export function WorkQueue({ items, isPending, isError, onRetry, className }: WorkQueueProps) {
  const [filter, setFilter] = useState<QueueFilter>('all')
  const visiveis = filterQueue(items, filter)

  return (
    <section aria-labelledby="fila-titulo" className={cn('space-y-3', className)}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="fila-titulo" className="text-lg font-semibold">
          Fila de trabalho
        </h2>
        <SegmentedControl
          value={filter}
          onChange={setFilter}
          options={filtros}
          label="Filtrar a fila de trabalho"
        />
      </div>

      <div
        className="overflow-hidden rounded-[10px] border bg-card shadow-sm"
        aria-busy={isPending}
      >
        {isError ? (
          <div className="p-4">
            <ErrorState
              error={new Error('Falha ao carregar a agenda.')}
              title="Não foi possível carregar os prazos"
              onRetry={onRetry}
            />
          </div>
        ) : isPending ? (
          <div className="space-y-2 p-4" role="status" aria-label="Carregando a fila">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} className="h-12" style={{ animationDelay: `${i * 0.15}s` }} />
            ))}
          </div>
        ) : visiveis.length === 0 ? (
          <EmptyState
            icon={CalendarCheck}
            title={
              filter === 'ai' ? 'Nenhuma sugestão da IA para revisar' : 'Nada urgente por aqui'
            }
            description={
              filter === 'all'
                ? 'Prazos e audiências dos próximos dias aparecem aqui.'
                : 'Troque o filtro para ver o resto da fila.'
            }
          />
        ) : (
          <ul className="divide-y">
            {visiveis.map((item) => (
              <li key={item.id}>
                <Link
                  href={item.href as never}
                  className="grid items-center gap-x-3 gap-y-1 px-4 py-3 outline-none hover:bg-accent focus-visible:bg-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset md:grid-cols-[118px_1fr_190px_72px]"
                >
                  <span>
                    <UrgencyBadge
                      level={item.urgency}
                      // Só prazo "vence"; audiência, reunião e tarefa simplesmente acontecem hoje.
                      label={item.urgency === 'today' && item.kind !== 'Prazo' ? 'Hoje' : undefined}
                    />
                  </span>
                  <span className="flex min-w-0 items-center gap-1.5 text-sm font-medium">
                    <span className="truncate">{item.title}</span>
                    {item.ai ? (
                      <>
                        <Sparkles className="size-3.5 shrink-0 text-ia-text" aria-hidden />
                        <span className="sr-only">(sugerido pela IA)</span>
                      </>
                    ) : null}
                  </span>
                  <span
                    className={cn(
                      'font-mono text-xs text-muted-foreground',
                      // No celular a linha "—" só ocupa espaço; na grade desktop ela alinha a coluna.
                      !item.processLabel && 'hidden md:inline'
                    )}
                  >
                    {item.processLabel ?? '—'}
                  </span>
                  <span
                    className={cn(
                      'font-mono text-sm md:text-right',
                      item.urgency === 'overdue' && 'font-medium text-destructive-foreground',
                      item.urgency === 'today' && 'font-medium text-warning-foreground'
                    )}
                  >
                    {prazoTexto(item)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

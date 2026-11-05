'use client'

import { routes } from '@/config/routes'
import { KpiCard } from '@/components/shared/KpiCard'
import { formatHora } from '@/features/agenda/dates'
import { isSameDay } from '../time'
import { buildQueue, prazosDoDia } from '../workQueue'
import type { DashboardSummary } from '../types'
import type { useDashboardWork } from '../hooks'
import { WorkQueue } from './WorkQueue'

type Work = ReturnType<typeof useDashboardWork>

interface AdvogadoPanelProps {
  variant: 'A' | 'B'
  metrics: DashboardSummary['metrics']
  work: Work
  now: Date | null
}

/** "às 18:00" com um prazo; "próximo às 18:00" com vários; "nenhum hoje" sem prazo. */
function dicaPrazosHoje(prazos: ReturnType<typeof prazosDoDia>, now: Date) {
  if (prazos.length === 0) return 'nenhum hoje'
  const proximo =
    prazos.find((prazo) => new Date(prazo.inicio) >= now) ?? prazos[prazos.length - 1]!

  return prazos.length === 1
    ? `às ${formatHora(proximo.inicio)}`
    : `próximo às ${formatHora(proximo.inicio)}`
}

const plural = (n: number, um: string, varios: string) => (n === 1 ? `1 ${um}` : `${n} ${varios}`)

export function AdvogadoPanel({ variant, metrics, work, now }: AdvogadoPanelProps) {
  const agora = now ?? new Date(0)
  const prazosHoje = now ? prazosDoDia(work.eventos, now) : []
  const fila = now
    ? buildQueue({
        events: work.eventos,
        processNumbers: work.processNumbers,
        reviews: work.reviews,
        now,
      })
    : []
  const paraRevisar = metrics.pendingReviews.value

  const queue = (
    <WorkQueue
      items={fila}
      isPending={work.isPending}
      isError={work.isError}
      onRetry={work.refetch}
    />
  )

  const kpis = [
    <KpiCard
      key="atrasadas"
      label="Tarefas atrasadas"
      value={null}
      hint="disponível com o módulo de tarefas"
      tone="danger"
      compact={variant === 'B'}
    />,
    <KpiCard
      key="prazos"
      label="Prazos hoje"
      value={work.isPending ? null : prazosHoje.length}
      hint={work.isPending ? 'carregando…' : dicaPrazosHoje(prazosHoje, agora)}
      tone="warning"
      href={routes.agenda}
      hrefLabel="Prazos hoje — abrir a agenda"
      compact={variant === 'B'}
    />,
    <KpiCard
      key="revisar"
      label="Para revisar"
      value={paraRevisar}
      hint="sugestões da IA"
      tone="ia"
      href={`${routes.publications}?status=untreated`}
      hrefLabel="Para revisar — ver as publicações"
      compact={variant === 'B'}
    />,
    <KpiCard
      key="alertas"
      label="Alertas"
      value={metrics.unreadAlerts.value}
      hint="não lidos"
      tone="brand"
      href={routes.alerts}
      hrefLabel="Alertas — ver a lista"
      compact={variant === 'B'}
    />,
  ]

  if (variant === 'A') {
    return (
      <div className="space-y-6">
        <section
          aria-label="Indicadores"
          className="grid grid-cols-[repeat(auto-fit,minmax(125px,1fr))] gap-3"
        >
          {kpis}
        </section>
        {queue}
      </div>
    )
  }

  const hojeDaAgenda = now
    ? work.eventos
        .filter((evento) => isSameDay(new Date(evento.inicio), now))
        .sort((a, b) => a.inicio.localeCompare(b.inicio))
    : []

  return (
    <div className="space-y-8">
      <p className="max-w-3xl text-[22px] leading-snug">
        {work.isPending ? (
          'Carregando o seu dia…'
        ) : (
          <>
            Hoje {prazosHoje.length === 1 ? 'vence ' : 'vencem '}
            <strong className="font-semibold">
              {plural(prazosHoje.length, 'prazo', 'prazos')}
            </strong>
            , e{' '}
            <strong className="font-semibold">
              {plural(paraRevisar, 'publicação aguarda', 'publicações aguardam')}
            </strong>{' '}
            sua revisão. A Athena acompanha as publicações do CNJ.
          </>
        )}
      </p>

      <section aria-label="Indicadores" className="grid grid-cols-2 divide-x md:grid-cols-4">
        {kpis}
      </section>

      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_280px]">
        {queue}
        <section aria-labelledby="agenda-hoje-titulo" className="space-y-3">
          <h2 id="agenda-hoje-titulo" className="text-lg font-semibold">
            Agenda de hoje
          </h2>
          {hojeDaAgenda.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nada marcado para hoje.</p>
          ) : (
            <ul className="space-y-2">
              {hojeDaAgenda.map((evento) => (
                <li key={evento.id} className="flex gap-3 text-sm">
                  <span
                    className={
                      evento.tipo === 'Prazo'
                        ? 'font-mono font-medium text-destructive-foreground'
                        : 'font-mono text-muted-foreground'
                    }
                  >
                    {evento.diaInteiro ? 'dia todo' : formatHora(evento.inicio)}
                  </span>
                  <span>{evento.titulo}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}

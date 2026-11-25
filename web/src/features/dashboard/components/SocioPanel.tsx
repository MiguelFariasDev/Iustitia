'use client'

import { KpiCard } from '@/components/shared/KpiCard'
import { UrgencyBadge } from '@/components/shared/UrgencyBadge'
import { formatHora } from '@/features/agenda/dates'
import { routes } from '@/config/routes'
import { TarefasEmAtraso } from '@/features/tarefas/components/TarefasEmAtraso'
import { useTarefasComEstado } from '@/features/tarefas/hooks'
import { tarefasAtrasadas } from '@/features/tarefas/kanban'
import { prazosDaSemana, urgencyOf } from '../workQueue'
import type { useDashboardWork } from '../hooks'
import { DemoSection } from './DemoSection'

const diaCurto = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' })

export function SocioPanel({
  work,
  now,
}: {
  work: ReturnType<typeof useDashboardWork>
  now: Date | null
}) {
  const prazos = now ? prazosDaSemana(work.eventos, now) : []
  const tarefas = useTarefasComEstado()
  const atrasadas = now ? tarefasAtrasadas(tarefas.tarefas, now) : []

  return (
    <div className="space-y-6">
      <section
        aria-label="Indicadores"
        className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3"
      >
        <KpiCard
          label="Prazos na semana"
          value={work.isPending ? null : prazos.length}
          hint="próximos 7 dias"
          tone="warning"
          href={routes.agenda}
          hrefLabel="Prazos na semana — abrir a agenda"
        />
        <KpiCard
          label="Tarefas em atraso"
          value={tarefas.isPending || tarefas.isError || !now ? null : atrasadas.length}
          hint={tarefas.isError ? 'não foi possível carregar' : 'do escritório inteiro'}
          tone="danger"
          href={routes.tasks}
          hrefLabel="Tarefas em atraso — abrir o quadro de tarefas"
        />
        <KpiCard
          label="Prazos cumpridos"
          value={null}
          hint="disponível com o módulo de tarefas"
          tone="neutral"
        />
      </section>

      <TarefasEmAtraso
        tarefas={tarefas.tarefas}
        agora={now}
        isPending={tarefas.isPending}
        isError={tarefas.isError}
        onRetry={() => void tarefas.refetch()}
      />

      <DemoSection id="riscos-titulo" title="Riscos de prazo">
        {work.isPending ? (
          <p className="text-sm text-muted-foreground">Carregando os prazos…</p>
        ) : prazos.length === 0 ? (
          <p className="py-2 text-sm text-muted-foreground">Nenhum prazo nos próximos 7 dias.</p>
        ) : (
          <ul className="divide-y">
            {prazos.map((prazo) => (
              <li key={prazo.id} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0">
                <UrgencyBadge level={now ? urgencyOf(new Date(prazo.inicio), now) : 'week'} />
                <span className="min-w-0 flex-1 truncate text-sm font-medium">{prazo.titulo}</span>
                <span className="font-mono text-xs text-muted-foreground">
                  {diaCurto.format(new Date(prazo.inicio))}
                  {prazo.diaInteiro ? '' : ` ${formatHora(prazo.inicio)}`}
                </span>
              </li>
            ))}
          </ul>
        )}
      </DemoSection>
    </div>
  )
}

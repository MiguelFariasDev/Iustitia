'use client'

import { useState } from 'react'
import { KpiCard } from '@/components/shared/KpiCard'
import { ToneOutlineButton } from '@/components/shared/ToneOutlineButton'
import { UrgencyBadge } from '@/components/shared/UrgencyBadge'
import { formatHora } from '@/features/agenda/dates'
import { routes } from '@/config/routes'
import { notify } from '@/lib/notify'
import { aprovacoesIniciais, brl, CARGA_MAXIMA, cargaEquipe, financeiroSocio } from '../demo'
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
  const [aprovacoes, setAprovacoes] = useState(aprovacoesIniciais)
  const prazos = now ? prazosDaSemana(work.eventos, now) : []

  function decidir(id: string, tipo: string) {
    setAprovacoes((lista) => lista.filter((item) => item.id !== id))
    notify.success(tipo === 'Peça' ? 'Peça enviada para sua revisão' : 'Aprovado')
  }

  return (
    <div className="space-y-6">
      <section
        aria-label="Indicadores"
        className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3"
      >
        <KpiCard
          label="Receita do mês"
          value={brl(financeiroSocio.receitaMes)}
          hint="demonstração"
          tone="brand"
        />
        <KpiCard
          label="Em atraso"
          value={brl(financeiroSocio.emAtraso)}
          hint="demonstração"
          tone="danger"
        />
        <KpiCard
          label="Prazos na semana"
          value={work.isPending ? null : prazos.length}
          hint="próximos 7 dias"
          tone="warning"
          href={routes.agenda}
          hrefLabel="Prazos na semana — abrir a agenda"
        />
        <KpiCard
          label="Prazos cumpridos"
          value={null}
          hint="disponível com o módulo de tarefas"
          tone="neutral"
        />
      </section>

      <div className="grid gap-6 lg:grid-cols-2">
        <DemoSection id="aprovacoes-titulo" title="Aprovações pendentes" demo>
          {aprovacoes.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">
              Nenhuma aprovação pendente.
            </p>
          ) : (
            <ul className="divide-y">
              {aprovacoes.map((item) => (
                <li key={item.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{item.titulo}</span>
                    <span className="block text-xs text-muted-foreground">
                      {item.tipo} · {item.solicitante}
                    </span>
                  </span>
                  <ToneOutlineButton
                    tone={item.tipo === 'Peça' ? 'ia' : 'ok'}
                    onClick={() => decidir(item.id, item.tipo)}
                    aria-label={`${item.tipo === 'Peça' ? 'Revisar' : 'Aprovar'}: ${item.titulo}`}
                  >
                    {item.tipo === 'Peça' ? 'Revisar' : 'Aprovar'}
                  </ToneOutlineButton>
                </li>
              ))}
            </ul>
          )}
        </DemoSection>

        <DemoSection id="carga-titulo" title="Carga da equipe" demo>
          <ul className="space-y-3">
            {cargaEquipe.map((pessoa) => (
              <li key={pessoa.nome} className="space-y-1">
                <div className="flex justify-between text-sm">
                  <span>{pessoa.nome}</span>
                  <span className="font-mono text-muted-foreground">{pessoa.tarefas} tarefas</span>
                </div>
                <div
                  role="progressbar"
                  aria-label={`Carga de ${pessoa.nome}`}
                  aria-valuemin={0}
                  aria-valuemax={CARGA_MAXIMA}
                  aria-valuenow={pessoa.tarefas}
                  className="h-2 overflow-hidden rounded-full bg-muted"
                >
                  <div
                    className="h-full rounded-full bg-brand"
                    style={{ width: `${Math.min(100, (pessoa.tarefas / CARGA_MAXIMA) * 100)}%` }}
                  />
                </div>
              </li>
            ))}
          </ul>
        </DemoSection>
      </div>

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

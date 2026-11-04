import { Briefcase, CalendarClock, CheckCheck } from 'lucide-react'
import { MetricCard } from '@/components/shared/MetricCard'
import { Card, CardContent } from '@/components/ui/card'
import { routes } from '@/config/routes'
import { descreverSerie } from '../descricao'
import { paleta } from '../palette'
import type { Indicadores } from '../types'

interface KpiCardsProps {
  kpis: Indicadores['kpis']
  processosPorArea: Indicadores['processosPorArea']
}

/** Pizza em miniatura feita só com `conic-gradient`: não carrega o Recharts para 4 fatias. */
function MiniPizza({ itens }: { itens: Indicadores['processosPorArea'] }) {
  const total = itens.reduce((soma, i) => soma + i.total, 0)
  let acumulado = 0
  const fatias = itens.map((item, i) => {
    const inicio = (acumulado / total) * 100
    acumulado += item.total
    return `${paleta[i % paleta.length]} ${inicio}% ${(acumulado / total) * 100}%`
  })

  return (
    <div
      role="img"
      aria-label={descreverSerie('Processos por área', itens)}
      className="size-16 shrink-0 rounded-full"
      style={{ background: total > 0 ? `conic-gradient(${fatias.join(', ')})` : undefined }}
    />
  )
}

export function KpiCards({ kpis, processosPorArea }: KpiCardsProps) {
  const maior = [...processosPorArea].sort((a, b) => b.total - a.total)[0]

  return (
    <section aria-labelledby="kpis-heading">
      <h2 id="kpis-heading" className="sr-only">
        Indicadores principais
      </h2>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          label="Total de processos ativos"
          icon={Briefcase}
          value={kpis.processosAtivos}
          href={`${routes.processes}?status=Ativo`}
          hrefLabel="Processos ativos — ver a lista"
        />

        <Card>
          <CardContent className="space-y-3 px-5">
            <p className="text-sm font-medium text-muted-foreground">Processos por área</p>
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-3xl font-semibold tracking-tight">{maior?.rotulo ?? '—'}</p>
                <p className="text-xs text-muted-foreground">
                  {maior ? `${maior.total} processos — a maior área` : 'Sem processos'}
                </p>
              </div>
              <MiniPizza itens={processosPorArea} />
            </div>
          </CardContent>
        </Card>

        <MetricCard
          label="Prazos vencendo"
          icon={CalendarClock}
          value={kpis.prazosVencendo}
          hint="nos próximos 7 dias"
          increaseIsGood={false}
        />

        <MetricCard
          label="Tarefas concluídas"
          icon={CheckCheck}
          value={kpis.tarefasConcluidas}
          hint="no período"
        />
      </div>
    </section>
  )
}

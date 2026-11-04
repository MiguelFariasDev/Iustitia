'use client'

import { CalendarClock, CheckCheck, Newspaper } from 'lucide-react'
import { MetricCard } from '@/components/shared/MetricCard'
import { Skeleton } from '@/components/ui/skeleton'
import { routes } from '@/config/routes'
import { usePublicacaoSummary } from '../hooks'

/**
 * Cartões do topo. Cada um leva à lista JÁ FILTRADA pelo que conta — o clique abre o mesmo
 * número que o cartão mostrava. O mini gráfico de tendência não existe: o backend ainda não
 * agrega por dia, e inventar uma série seria pior que não mostrar (ver relatório da etapa).
 */
export function PublicacaoResumoCards() {
  const { data, isPending, isError } = usePublicacaoSummary()

  if (isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-3" role="status" aria-label="Carregando resumo">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-32 rounded-xl" />
        ))}
      </div>
    )
  }

  // Resumo é complemento da lista: se falhar, a lista continua útil — esconder o cartão é
  // melhor que bloquear a tela inteira por causa de um contador.
  if (isError || !data) return null

  return (
    <section aria-labelledby="publicacoes-resumo" className="grid gap-4 sm:grid-cols-3">
      <h2 id="publicacoes-resumo" className="sr-only">
        Resumo das publicações
      </h2>
      <MetricCard
        label="Não tratadas de hoje"
        value={data.untreatedToday}
        icon={CalendarClock}
        increaseIsGood={false}
        hint="divulgadas hoje"
        href={`${routes.publications}?status=untreated`}
        hrefLabel="Não tratadas de hoje — ver as não tratadas"
      />
      <MetricCard
        label="Não tratadas"
        value={data.untreatedTotal}
        icon={Newspaper}
        increaseIsGood={false}
        hint="aguardando revisão"
        href={`${routes.publications}?status=untreated`}
        hrefLabel="Não tratadas — ver a lista"
      />
      <MetricCard
        label="Tratadas esta semana"
        value={data.treatedThisWeek}
        icon={CheckCheck}
        hint="desde segunda-feira"
        href={`${routes.publications}?status=treated`}
        hrefLabel="Tratadas esta semana — ver a lista"
      />
    </section>
  )
}

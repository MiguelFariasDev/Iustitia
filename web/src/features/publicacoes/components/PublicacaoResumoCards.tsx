'use client'

import { KpiCard } from '@/components/shared/KpiCard'
import { Skeleton } from '@/components/ui/skeleton'
import { routes } from '@/config/routes'
import { useNow } from '@/hooks/useNow'
import { usePublicacaoSummary } from '../hooks'
import { isoDate, mondayOf } from '../view'

/**
 * Cartões do topo (v4). Cada um leva à lista JÁ FILTRADA pelo que conta, e o número da lista é o
 * do cartão: "não tratadas de hoje" filtra também pela data de hoje, e "tratadas esta semana", pela
 * semana — sem as datas o clique abriria o total, um número diferente do que o cartão mostrava.
 * As datas são as do fuso LOCAL da pessoa; antes de hidratar (sem "agora") os links saem sem data.
 */
export function PublicacaoResumoCards() {
  const { data, isPending, isError } = usePublicacaoSummary()
  const now = useNow()

  if (isPending) {
    return (
      <div className="grid gap-3 sm:grid-cols-3" role="status" aria-label="Carregando resumo">
        {[0, 1, 2].map((i) => (
          <Skeleton
            key={i}
            className="h-32 rounded-[10px]"
            style={{ animationDelay: `${i * 0.1}s` }}
          />
        ))}
      </div>
    )
  }

  // Resumo é complemento da lista: se falhar, a lista continua útil — esconder o cartão é
  // melhor que bloquear a tela inteira por causa de um contador.
  if (isError || !data) return null

  const hoje = now ? isoDate(now) : null
  const segunda = now ? isoDate(mondayOf(now)) : null
  const naoTratadasHoje = new URLSearchParams({ status: 'untreated' })
  if (hoje) {
    naoTratadasHoje.set('from', hoje)
    naoTratadasHoje.set('to', hoje)
  }
  const tratadasSemana = new URLSearchParams({ status: 'treated' })
  if (segunda) tratadasSemana.set('from', segunda)

  return (
    <section aria-labelledby="publicacoes-resumo" className="grid gap-3 sm:grid-cols-3">
      <h2 id="publicacoes-resumo" className="sr-only">
        Resumo das publicações
      </h2>
      <KpiCard
        label="Não tratadas hoje"
        value={data.untreatedToday}
        hint="divulgadas hoje"
        tone="warning"
        href={`${routes.publications}?${naoTratadasHoje}`}
        hrefLabel="Não tratadas hoje — ver a lista"
      />
      <KpiCard
        label="Aguardando revisão"
        value={data.untreatedTotal}
        hint="revisão humana pendente"
        tone="ia"
        href={`${routes.publications}?status=untreated`}
        hrefLabel="Aguardando revisão — ver a lista"
      />
      <KpiCard
        label="Tratadas na semana"
        value={data.treatedThisWeek}
        hint="desde segunda-feira"
        tone="brand"
        href={`${routes.publications}?${tratadasSemana}`}
        hrefLabel="Tratadas na semana — ver a lista"
      />
    </section>
  )
}

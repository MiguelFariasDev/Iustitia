'use client'

import { useSuspenseQuery } from '@tanstack/react-query'
import { Suspense } from 'react'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { apiClient } from '@/lib/api/client'
import { useNow } from '@/hooks/useNow'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import { useDashboardWork } from '../hooks'
import { personaFor } from '../persona'
import { dashboardSummaryQuery } from '../queries'
import { dataPorExtenso, primeiroNome, saudacao } from '../time'
import { AdvogadoPanel } from './AdvogadoPanel'
import { DashboardSkeleton } from './DashboardSkeleton'
import { FinanceiroPanel } from './FinanceiroPanel'
import { SecretariaPanel } from './SecretariaPanel'
import { SocioPanel } from './SocioPanel'

const variantes = [
  { value: 'A', label: 'Variação A' },
  { value: 'B', label: 'Variação B' },
] as const

/**
 * Painel por perfil (protótipo v4). O perfil vem da sessão — Sócio, Advogado(a), Secretária ou
 * Financeiro, ver `persona.ts` — e cada um vê um painel diferente. Prazos e publicações a revisar
 * são dados reais; o que não tem backend (financeiro, aprovações, carga da equipe) aparece com o
 * selo "Demonstração".
 */
/** Só o advogado vê o resumo (publicações, alertas): é o que a API lhe permite ler. */
function PainelAdvogado({ now }: { now: Date | null }) {
  const { data } = useSuspenseQuery(dashboardSummaryQuery(apiClient))
  const variant = useUiStore((s) => s.dashboardVariant)
  const work = useDashboardWork(now, 'mine')

  return <AdvogadoPanel variant={variant} metrics={data.metrics} work={work} now={now} />
}

function PainelSocio({ now }: { now: Date | null }) {
  // O sócio só precisa dos prazos do escritório; o resto do painel dele é demonstração.
  const work = useDashboardWork(now, 'all', { withQueue: false })

  return <SocioPanel work={work} now={now} />
}

export function DashboardView() {
  const user = useAuthStore((s) => s.user)
  const now = useNow()
  const persona = personaFor(user?.role, user?.accountType)
  const variant = useUiStore((s) => s.dashboardVariant)
  const setVariant = useUiStore((s) => s.setDashboardVariant)
  const nome = primeiroNome(user?.name)

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="space-y-1">
          <p className="min-h-4 text-xs font-medium text-muted-foreground">
            {now ? dataPorExtenso(now) : ''}
          </p>
          <h1 className="text-[33px] leading-tight font-semibold">
            {now ? saudacao(now) : 'Olá'}
            {nome ? `, ${nome}.` : '.'}
          </h1>
        </div>
        {persona === 'advogado' ? (
          <SegmentedControl
            value={variant}
            onChange={setVariant}
            options={variantes}
            label="Variação do painel"
          />
        ) : null}
      </header>

      {persona === 'advogado' ? (
        <Suspense fallback={<DashboardSkeleton />}>
          <PainelAdvogado now={now} />
        </Suspense>
      ) : persona === 'socio' ? (
        <PainelSocio now={now} />
      ) : persona === 'financeiro' ? (
        <FinanceiroPanel />
      ) : (
        <SecretariaPanel />
      )}
    </div>
  )
}

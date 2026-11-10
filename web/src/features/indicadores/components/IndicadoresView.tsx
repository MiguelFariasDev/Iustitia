'use client'

import dynamic from 'next/dynamic'
import { useState } from 'react'
import { DemoNotice } from '@/components/shared/DemoNotice'
import { ErrorState } from '@/components/shared/ErrorState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useIndicadores } from '../hooks'
import { periodoLabels, periodos, type Periodo } from '../types'
import { KpiCards } from './KpiCards'
import { ProdutividadeTable } from './ProdutividadeTable'

// Recharts é pesado e fica abaixo da dobra: carrega sob demanda, só no cliente. O Next exige
// que as opções do `dynamic` sejam um objeto LITERAL (não dá para compartilhá-las numa const).
const ProcessosPorAreaChart = dynamic(
  () => import('./ProcessosPorAreaChart').then((m) => m.ProcessosPorAreaChart),
  { ssr: false, loading: () => <div className="h-72 animate-iu-pulse rounded-lg bg-border" /> }
)
const ProcessosPorStatusChart = dynamic(
  () => import('./ProcessosPorStatusChart').then((m) => m.ProcessosPorStatusChart),
  { ssr: false, loading: () => <div className="h-72 animate-iu-pulse rounded-lg bg-border" /> }
)
const EvolucaoPublicacoesChart = dynamic(
  () => import('./EvolucaoPublicacoesChart').then((m) => m.EvolucaoPublicacoesChart),
  { ssr: false, loading: () => <div className="h-72 animate-iu-pulse rounded-lg bg-border" /> }
)

export function IndicadoresView() {
  const [periodo, setPeriodo] = useState<Periodo>('mes')
  const { data, isPending, isError, error, refetch, isPlaceholderData } = useIndicadores(periodo)

  return (
    <div className="space-y-6">
      <DemoNotice modulo="Indicadores">
        prazos e tarefas só passam a existir nas próximas fases, então estes números são fictícios.
      </DemoNotice>

      <div className="flex flex-wrap items-center gap-3">
        <span className="text-sm font-medium">Período</span>
        <SegmentedControl
          label="Período"
          value={periodo}
          onChange={setPeriodo}
          options={periodos.map((valor) => ({ value: valor, label: periodoLabels[valor] }))}
        />
      </div>

      {isError ? (
        <ErrorState
          error={error}
          title="Não foi possível carregar os indicadores"
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <div className="flex justify-center py-12">
          <LoadingSpinner label="Carregando indicadores…" />
        </div>
      ) : (
        <div className="animate-iu-in space-y-6" aria-busy={isPlaceholderData}>
          <KpiCards kpis={data.kpis} processosPorArea={data.processosPorArea} />

          <div className="grid gap-6 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  <h2>Processos por área</h2>
                </CardTitle>
                <CardDescription>Carteira atual, independente do período.</CardDescription>
              </CardHeader>
              <CardContent>
                <ProcessosPorAreaChart dados={data.processosPorArea} />
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  <h2>Processos por status</h2>
                </CardTitle>
                <CardDescription>Carteira atual, independente do período.</CardDescription>
              </CardHeader>
              <CardContent>
                <ProcessosPorStatusChart dados={data.processosPorStatus} />
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">
                <h2>Evolução de publicações</h2>
              </CardTitle>
              <CardDescription>
                Publicações capturadas — {periodoLabels[periodo].toLowerCase()}.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <EvolucaoPublicacoesChart dados={data.evolucaoPublicacoes} />
            </CardContent>
          </Card>

          <section aria-labelledby="produtividade-heading" className="space-y-3">
            <h2 id="produtividade-heading" className="text-base font-semibold">
              Produtividade por advogado
            </h2>
            <ProdutividadeTable dados={data.produtividade} />
          </section>
        </div>
      )}
    </div>
  )
}

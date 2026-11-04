'use client'

import { DemoNotice } from '@/components/shared/DemoNotice'
import { ErrorState } from '@/components/shared/ErrorState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { useFinanceiroResumo, useFluxoCaixa, useLancamentos } from '../hooks'
import { FinanceiroResumo } from './FinanceiroResumo'
import { FinanceiroTabs } from './FinanceiroTabs'

export function FinanceiroView() {
  const resumo = useFinanceiroResumo()
  const lancamentos = useLancamentos()
  const fluxo = useFluxoCaixa()

  const erro = resumo.error ?? lancamentos.error ?? fluxo.error

  return (
    <div className="space-y-6">
      <DemoNotice modulo="Financeiro">
        nada aqui é do seu escritório. Lançamentos novos valem só até recarregar a página.
      </DemoNotice>

      {erro ? (
        <ErrorState
          error={erro}
          title="Não foi possível carregar o financeiro"
          onRetry={() => {
            void resumo.refetch()
            void lancamentos.refetch()
            void fluxo.refetch()
          }}
        />
      ) : !resumo.data || !lancamentos.data || !fluxo.data ? (
        <div className="flex justify-center py-12">
          <LoadingSpinner label="Carregando financeiro…" />
        </div>
      ) : (
        <>
          <FinanceiroResumo resumo={resumo.data} />
          <FinanceiroTabs lancamentos={lancamentos.data} fluxoCaixa={fluxo.data} />
        </>
      )}
    </div>
  )
}

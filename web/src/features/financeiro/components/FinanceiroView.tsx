'use client'

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
      <p
        role="note"
        className="rounded-lg border border-dashed px-3 py-2 text-sm text-muted-foreground"
      >
        <strong>Lançamentos salvos neste navegador.</strong> O financeiro ainda não tem servidor: o
        que você lança fica só aqui, na sua conta, e não aparece em outros aparelhos nem para outras
        pessoas do escritório.
      </p>

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

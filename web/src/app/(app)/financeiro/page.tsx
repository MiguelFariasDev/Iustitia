import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { FinanceiroDashboard } from '@/features/financeiro/components/FinanceiroDashboard'

export const metadata: Metadata = { title: 'Financeiro' }

export default function FinanceiroPage() {
  return (
    <>
      <PageHeader
        title="Financeiro"
        description="Honorários, contas a pagar e a receber e fluxo de caixa."
      />
      <FinanceiroDashboard />
    </>
  )
}

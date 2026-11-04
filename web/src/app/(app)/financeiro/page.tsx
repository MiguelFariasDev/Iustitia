import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { FinanceiroView } from '@/features/financeiro/components/FinanceiroView'
import { NovoLancamentoModal } from '@/features/financeiro/components/NovoLancamentoModal'

export const metadata: Metadata = { title: 'Financeiro' }

export default function FinanceiroPage() {
  return (
    <>
      <PageHeader
        title="Financeiro"
        description="Honorários, contas a pagar e a receber e fluxo de caixa."
        actions={<NovoLancamentoModal />}
      />
      <FinanceiroView />
    </>
  )
}

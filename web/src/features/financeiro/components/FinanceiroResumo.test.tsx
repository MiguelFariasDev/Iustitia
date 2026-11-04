import { screen, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { FinanceiroResumo } from './FinanceiroResumo'

const resumo = {
  faturamentoMes: 1500,
  honorariosAReceber: 11700,
  contasAPagar: 3220,
  contasAReceber: 640,
  saldo: 3500,
}

function cartao(nome: string) {
  return screen.getByText(nome).closest('[data-slot="card"]') as HTMLElement
}

describe('FinanceiroResumo', () => {
  it('mostra os quatro totais formatados em reais', () => {
    renderWithProviders(<FinanceiroResumo resumo={resumo} />)

    expect(within(cartao('Faturamento do mês')).getByText(/R\$\s*1\.500,00/)).toBeInTheDocument()
    expect(within(cartao('Honorários a receber')).getByText(/R\$\s*11\.700,00/)).toBeInTheDocument()
    expect(within(cartao('Contas a pagar')).getByText(/R\$\s*3\.220,00/)).toBeInTheDocument()
    expect(within(cartao('Saldo')).getByText(/R\$\s*3\.500,00/)).toBeInTheDocument()
  })

  it('é uma região nomeada', () => {
    renderWithProviders(<FinanceiroResumo resumo={resumo} />)

    expect(screen.getByRole('region', { name: 'Resumo financeiro' })).toBeInTheDocument()
  })

  it('saldo negativo é dito por texto, não só pela cor', () => {
    renderWithProviders(<FinanceiroResumo resumo={{ ...resumo, saldo: -250 }} />)

    expect(within(cartao('Saldo')).getByText('(negativo)')).toBeInTheDocument()
  })

  it('saldo positivo não anuncia "negativo"', () => {
    renderWithProviders(<FinanceiroResumo resumo={resumo} />)

    expect(screen.queryByText('(negativo)')).not.toBeInTheDocument()
  })
})

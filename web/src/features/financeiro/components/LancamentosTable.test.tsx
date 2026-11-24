import { screen, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { lancamentosDeTeste } from '../fixtures'
import { LancamentosTable } from './LancamentosTable'

const headers = () => screen.getAllByRole('columnheader').map((h) => h.textContent?.trim())

describe('LancamentosTable', () => {
  it('honorários: cliente, processo, valor, vencimento e status — só os honorários', () => {
    renderWithProviders(<LancamentosTable tipo="Honorario" lancamentos={lancamentosDeTeste()} />)

    expect(headers()).toEqual(['Cliente', 'Processo', 'Valor', 'Vencimento', 'Status', 'Ações'])
    expect(screen.getAllByRole('row')).toHaveLength(1 + 3)
    expect(screen.queryByText('Aluguel do escritório')).not.toBeInTheDocument()
  })

  it('contas a pagar mostram o fornecedor', () => {
    renderWithProviders(<LancamentosTable tipo="ContaPagar" lancamentos={lancamentosDeTeste()} />)

    expect(headers()).toEqual(['Descrição', 'Fornecedor', 'Valor', 'Vencimento', 'Status', 'Ações'])
    const linha = screen.getByRole('row', { name: /Aluguel do escritório/ })
    expect(within(linha).getByText('Imobiliária Central')).toBeInTheDocument()
    expect(within(linha).getByText(/R\$\s*2\.800,00/)).toBeInTheDocument()
  })

  it('contas a receber mostram o cliente', () => {
    renderWithProviders(<LancamentosTable tipo="ContaReceber" lancamentos={lancamentosDeTeste()} />)

    expect(headers()).toEqual(['Descrição', 'Cliente', 'Valor', 'Vencimento', 'Status', 'Ações'])
    expect(screen.getByText('Reembolso de despesas')).toBeInTheDocument()
  })

  it('o status aparece como texto', () => {
    renderWithProviders(<LancamentosTable tipo="Honorario" lancamentos={lancamentosDeTeste()} />)

    expect(screen.getByText('Atrasado')).toBeInTheDocument()
    expect(screen.getByText('Pago')).toBeInTheDocument()
    expect(screen.getByText('Pendente')).toBeInTheDocument()
  })

  it('sem lançamentos, mostra o estado vazio', () => {
    renderWithProviders(<LancamentosTable tipo="Honorario" lancamentos={[]} />)

    expect(screen.getByText('Nenhum lançamento')).toBeInTheDocument()
  })
})

describe('LancamentosTable — marcar como pago', () => {
  it('só os lançamentos em aberto têm o botão; o que já foi pago não', () => {
    renderWithProviders(<LancamentosTable tipo="Honorario" lancamentos={lancamentosDeTeste()} />)

    const emAberto = lancamentosDeTeste().filter(
      (l) => l.tipo === 'Honorario' && l.status !== 'Pago'
    )
    const pagos = lancamentosDeTeste().filter((l) => l.tipo === 'Honorario' && l.status === 'Pago')

    expect(screen.getAllByRole('button', { name: /marcar como pago/i })).toHaveLength(
      emAberto.length
    )
    expect(pagos.length).toBeGreaterThan(0)
  })
})

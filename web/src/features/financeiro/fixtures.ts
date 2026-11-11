import { daysFromNow } from '@/mocks/dates'
import type { Lancamento } from './types'

/**
 * Lançamentos de EXEMPLO para os testes. Não é importado pelo app: a tela começa vazia e só
 * mostra o que o usuário lançar (ver repositorio.ts).
 */
type Seed = Omit<Lancamento, 'vencimento' | 'pagoEm'> & { vence: number; pago?: boolean }

export function lancamentosDeTeste(): Lancamento[] {
  const base: Seed[] = [
    {
      id: '1',
      tipo: 'Honorario',
      descricao: 'Honorários iniciais — ação de cobrança',
      cliente: 'Maria Souza',
      fornecedor: null,
      processo: '0000832-35.2018.4.01.3202',
      valor: 3500,
      vence: 6,
      status: 'Pendente',
    },
    {
      id: '2',
      tipo: 'Honorario',
      descricao: 'Êxito — reclamação trabalhista',
      cliente: 'José Lima',
      fornecedor: null,
      processo: null,
      valor: 8200,
      vence: -20,
      status: 'Atrasado',
    },
    {
      id: '3',
      tipo: 'Honorario',
      descricao: 'Mensalidade consultoria',
      cliente: 'Padaria Boa Massa LTDA',
      fornecedor: null,
      processo: null,
      valor: 1500,
      vence: -2,
      status: 'Pago',
      pago: true,
    },
    {
      id: '4',
      tipo: 'ContaPagar',
      descricao: 'Aluguel do escritório',
      cliente: null,
      fornecedor: 'Imobiliária Central',
      processo: null,
      valor: 2800,
      vence: 4,
      status: 'Pendente',
    },
    {
      id: '5',
      tipo: 'ContaPagar',
      descricao: 'Custas judiciais',
      cliente: null,
      fornecedor: 'Tribunal de Justiça',
      processo: '0000832-35.2018.4.01.3202',
      valor: 420,
      vence: -7,
      status: 'Atrasado',
    },
    {
      id: '6',
      tipo: 'ContaReceber',
      descricao: 'Reembolso de despesas',
      cliente: 'José Lima',
      fornecedor: null,
      processo: null,
      valor: 640,
      vence: 8,
      status: 'Pendente',
    },
    {
      id: '7',
      tipo: 'ContaReceber',
      descricao: 'Parcela 2/3 — acordo',
      cliente: 'Padaria Boa Massa LTDA',
      fornecedor: null,
      processo: null,
      valor: 2000,
      vence: -12,
      status: 'Pago',
      pago: true,
    },
  ]

  return base.map(({ vence, pago, ...resto }) => ({
    ...resto,
    vencimento: daysFromNow(vence, 12),
    pagoEm: pago ? daysFromNow(0, 12) : null,
  }))
}

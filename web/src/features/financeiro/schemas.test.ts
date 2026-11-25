import { lancamentoFormDefaults, lancamentoFormSchema } from './schemas'
import { lancamentosToCsv } from './export'
import { lancamentosDeTeste } from './fixtures'

const valido = {
  ...lancamentoFormDefaults,
  descricao: 'Honorários',
  valor: 1500,
  vencimento: '2026-11-30',
}

describe('lancamentoFormSchema', () => {
  it('aceita o mínimo necessário', () => {
    expect(lancamentoFormSchema.safeParse(valido).success).toBe(true)
  })

  it.each([null, 0, -10])('recusa valor %s', (valor) => {
    const r = lancamentoFormSchema.safeParse({ ...valido, valor })

    expect(r.error?.issues[0]?.message).toBe('O valor deve ser maior que zero.')
  })

  it('exige descrição e vencimento', () => {
    const r = lancamentoFormSchema.safeParse({ ...valido, descricao: ' ', vencimento: '' })
    const mensagens = r.error?.issues.map((i) => i.message)

    expect(mensagens).toContain('Informe a descrição.')
    expect(mensagens).toContain('Informe o vencimento.')
  })
})

describe('lancamentosToCsv', () => {
  it('uma linha por lançamento, valor com vírgula decimal e cliente ou fornecedor', () => {
    const csv = lancamentosToCsv(lancamentosDeTeste())
    const linhas = csv.replace('﻿', '').split('\r\n')

    expect(linhas[0]).toBe('Tipo;Descrição;Cliente / Fornecedor;Processo;Valor;Vencimento;Status')
    expect(linhas).toHaveLength(1 + 7)
    // O csvCell compartilhado aspeia células com vírgula; o Excel lê "2800,00" como número.
    expect(csv).toContain('Conta a pagar;Aluguel do escritório;Imobiliária Central;;"2800,00";')
  })

  it('neutraliza fórmula em texto vindo do usuário', () => {
    const csv = lancamentosToCsv([
      { ...lancamentosDeTeste()[0]!, descricao: '=HYPERLINK("http://x")' },
    ])

    expect(csv).toContain("'=HYPERLINK")
  })
})

describe('séries de lançamentos', () => {
  it('parcelado divide o total e põe os centavos que sobram na última', async () => {
    const { previaSerie } = await import('./schemas')

    expect(previaSerie('Parcelado', 1000, 3)).toEqual({ valorParcela: 333.33, valorUltima: 333.34, total: 1000 })
    expect(previaSerie('Mensal', 1500, 12)).toEqual({ valorParcela: 1500, valorUltima: 1500, total: 18000 })
    expect(previaSerie('Unica', 1500, 12)).toBeNull()
  })

  it('vencimento em dia que o mês não tem cai no último dia, sem arrastar os seguintes', async () => {
    const { vencimentoDaParcela } = await import('./schemas')
    const dia = (n: number) => vencimentoDaParcela('2027-01-31', n)!.toLocaleDateString('pt-BR')

    expect([dia(1), dia(2), dia(3)]).toEqual(['31/01/2027', '28/02/2027', '31/03/2027'])
  })

  it('exige de 2 a 60 parcelas só quando é série', async () => {
    const { lancamentoFormSchema, lancamentoFormDefaults } = await import('./schemas')
    const base = { ...lancamentoFormDefaults, descricao: 'Contrato', valor: 1200, vencimento: '2026-12-10' }

    expect(lancamentoFormSchema.safeParse({ ...base, parcelas: 1 }).success).toBe(true)
    expect(lancamentoFormSchema.safeParse({ ...base, recorrencia: 'Mensal', parcelas: 1 }).success).toBe(false)
    expect(lancamentoFormSchema.safeParse({ ...base, recorrencia: 'Parcelado', parcelas: 12 }).success).toBe(true)
    expect(lancamentoFormSchema.safeParse({ ...base, recorrencia: 'Parcelado', parcelas: 61 }).success).toBe(false)
  })
})

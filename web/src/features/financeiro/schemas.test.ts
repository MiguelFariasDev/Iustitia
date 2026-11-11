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

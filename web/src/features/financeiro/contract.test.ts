import { toFluxoCaixa, toLancamento, type LancamentoDto } from './contract'

function dto(overrides: Partial<LancamentoDto> = {}): LancamentoDto {
  return {
    id: 'l1',
    tipo: 'Honorario',
    descricao: 'Honorários',
    pessoa: 'Maria Souza',
    processo: null,
    valor: 1500.5,
    vencimento: '2026-11-24',
    status: 'Pendente',
    pagoEm: null,
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

describe('tradução do lançamento do servidor para a tela', () => {
  it('a pessoa vira cliente, exceto na conta a pagar, onde vira fornecedor', () => {
    expect(toLancamento(dto())).toMatchObject({ cliente: 'Maria Souza', fornecedor: null })
    expect(toLancamento(dto({ tipo: 'ContaPagar', pessoa: 'Gráfica' }))).toMatchObject({
      cliente: null,
      fornecedor: 'Gráfica',
    })
    expect(toLancamento(dto({ tipo: 'ContaReceber' }))).toMatchObject({ cliente: 'Maria Souza' })
  })

  it('a data de vencimento fica no meio-dia local, sem voltar um dia pelo fuso', () => {
    const { vencimento } = toLancamento(dto({ vencimento: '2026-11-24' }))
    const data = new Date(vencimento)

    expect([data.getFullYear(), data.getMonth() + 1, data.getDate()]).toEqual([2026, 11, 24])
  })

  it('status desconhecido nunca vira "Pago": cai em Pendente', () => {
    expect(toLancamento(dto({ status: 'Quitado?' })).status).toBe('Pendente')
    expect(toLancamento(dto({ status: 'Atrasado' })).status).toBe('Atrasado')
  })
})

describe('fluxo de caixa', () => {
  it('rotula o mês em português, na ordem recebida', () => {
    const fluxo = toFluxoCaixa([
      { ano: 2026, mes: 6, entradas: 0, saidas: 0 },
      { ano: 2026, mes: 11, entradas: 1000, saidas: 400 },
    ])

    expect(fluxo.map((m) => m.mes)).toEqual(['Jun', 'Nov'])
    expect(fluxo[1]).toMatchObject({ entradas: 1000, saidas: 400 })
  })
})

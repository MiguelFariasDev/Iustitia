import {
  calcularResumo,
  filtrarLancamentos,
  mockCreateLancamento,
  mockFluxoCaixa,
  mockListLancamentos,
  resetFinanceiroMock,
} from './mocks'

beforeEach(() => resetFinanceiroMock())

describe('calcularResumo', () => {
  it('separa o que está em aberto do que foi pago', () => {
    const r = calcularResumo(mockListLancamentos())
    expect(r.faturamentoMes).toBe(1500)
    expect(r.honorariosAReceber).toBe(3500 + 8200)
    expect(r.contasAPagar).toBe(2800 + 420)
    expect(r.contasAReceber).toBe(640)
    expect(r.saldo).toBe(1500 + 2000)
  })

  it('só conta como faturamento do mês o honorário pago no mês corrente', () => {
    const lista = mockListLancamentos()
    const daquiAUmaAno = new Date(new Date().getFullYear() + 1, 0, 15)

    expect(calcularResumo(lista, daquiAUmaAno).faturamentoMes).toBe(0)
  })

  it('um lançamento pago novo entra no faturamento e no saldo', () => {
    mockCreateLancamento({
      tipo: 'Honorario',
      descricao: 'Parecer',
      pessoa: 'Ana',
      processo: '',
      valor: 900,
      vencimento: '2026-11-10',
      status: 'Pago',
    })
    const r = calcularResumo(mockListLancamentos())

    expect(r.faturamentoMes).toBe(1500 + 900)
    expect(r.saldo).toBe(3500 + 900)
  })
})

describe('mockCreateLancamento', () => {
  it('conta a pagar guarda a pessoa como fornecedor; os demais como cliente', () => {
    const base = {
      descricao: 'x',
      processo: '',
      valor: 10,
      vencimento: '2026-11-10',
      status: 'Pendente' as const,
    }
    const pagar = mockCreateLancamento({ ...base, tipo: 'ContaPagar', pessoa: 'Gráfica' })
    const receber = mockCreateLancamento({ ...base, tipo: 'ContaReceber', pessoa: 'Maria' })

    expect(pagar).toMatchObject({ fornecedor: 'Gráfica', cliente: null })
    expect(receber).toMatchObject({ cliente: 'Maria', fornecedor: null })
  })

  it('campos em branco viram null', () => {
    const l = mockCreateLancamento({
      tipo: 'Honorario',
      descricao: 'x',
      pessoa: '  ',
      processo: '',
      valor: 10,
      vencimento: '2026-11-10',
      status: 'Pendente',
    })

    expect(l.cliente).toBeNull()
    expect(l.processo).toBeNull()
  })
})

describe('filtrarLancamentos', () => {
  const todos = () => mockListLancamentos()

  it('filtra por status', () => {
    const atrasados = filtrarLancamentos(todos(), { status: 'Atrasado', periodo: 'todos' })

    expect(atrasados.map((l) => l.id).sort()).toEqual(['2', '5'])
  })

  it('"próximos 30 dias" exclui o que já venceu', () => {
    const proximos = filtrarLancamentos(todos(), { status: null, periodo: 'proximos30' })

    expect(proximos.every((l) => l.status === 'Pendente')).toBe(true)
    expect(proximos).toHaveLength(3)
  })
})

describe('mockFluxoCaixa', () => {
  it('traz seis meses terminando no mês atual', () => {
    const fluxo = mockFluxoCaixa(new Date(2026, 10, 4))

    expect(fluxo.map((m) => m.mes)).toEqual(['Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov'])
  })
})

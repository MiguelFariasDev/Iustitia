import { useAuthStore } from '@/stores/authStore'
import { lancamentosDeTeste } from './fixtures'
import {
  calcularResumo,
  criarLancamento,
  filtrarLancamentos,
  fluxoDeCaixa,
  listarLancamentos,
} from './repositorio'
import type { LancamentoFormValues } from './schemas'

const base: LancamentoFormValues = {
  tipo: 'Honorario',
  descricao: 'x',
  pessoa: 'Ana',
  processo: '',
  valor: 10,
  vencimento: '2026-11-10',
  status: 'Pendente',
}

function entrarComo(id: string | null) {
  useAuthStore.setState({
    user: id ? ({ id, name: 'Teste', email: 't@t.com', role: 'Owner' } as never) : null,
  })
}

beforeEach(() => {
  window.localStorage.clear()
  entrarComo('user-1')
})

describe('começa vazio: nada inventado', () => {
  it('sem lançamentos do usuário, a lista, o resumo e o fluxo vêm zerados', () => {
    expect(listarLancamentos()).toEqual([])
    expect(calcularResumo(listarLancamentos())).toEqual({
      faturamentoMes: 0,
      honorariosAReceber: 0,
      contasAPagar: 0,
      contasAReceber: 0,
      saldo: 0,
    })
    expect(fluxoDeCaixa(listarLancamentos()).every((m) => m.entradas === 0 && m.saidas === 0)).toBe(
      true
    )
  })
})

describe('criarLancamento', () => {
  it('persiste e devolve na listagem, ordenado por vencimento', () => {
    criarLancamento({ ...base, descricao: 'depois', vencimento: '2026-12-01' })
    criarLancamento({ ...base, descricao: 'antes', vencimento: '2026-11-20' })

    expect(listarLancamentos(new Date(2026, 10, 10)).map((l) => l.descricao)).toEqual([
      'antes',
      'depois',
    ])
  })

  it('cada usuário vê só os próprios lançamentos', () => {
    criarLancamento(base)
    entrarComo('user-2')

    expect(listarLancamentos()).toEqual([])
  })

  it('conta a pagar guarda a pessoa como fornecedor; os demais como cliente', () => {
    const pagar = criarLancamento({ ...base, tipo: 'ContaPagar', pessoa: 'Gráfica' })
    const receber = criarLancamento({ ...base, tipo: 'ContaReceber', pessoa: 'Maria' })

    expect(pagar).toMatchObject({ fornecedor: 'Gráfica', cliente: null })
    expect(receber).toMatchObject({ cliente: 'Maria', fornecedor: null })
  })

  it('campos em branco viram null', () => {
    const l = criarLancamento({ ...base, pessoa: '  ' })

    expect(l.cliente).toBeNull()
    expect(l.processo).toBeNull()
  })

  it('pendente com vencimento passado aparece como atrasado', () => {
    criarLancamento({ ...base, vencimento: '2026-11-01' })

    expect(listarLancamentos(new Date(2026, 10, 10))[0]!.status).toBe('Atrasado')
    expect(listarLancamentos(new Date(2026, 10, 1))[0]!.status).toBe('Pendente')
  })

  it('um lançamento pago entra no faturamento do mês, no saldo e no fluxo de caixa', () => {
    criarLancamento({ ...base, valor: 900, status: 'Pago' })
    const lancamentos = listarLancamentos()
    const resumo = calcularResumo(lancamentos)

    expect(resumo.faturamentoMes).toBe(900)
    expect(resumo.saldo).toBe(900)
    expect(fluxoDeCaixa(lancamentos).at(-1)!.entradas).toBe(900)
  })
})

describe('calcularResumo', () => {
  it('separa o que está em aberto do que foi pago', () => {
    const r = calcularResumo(lancamentosDeTeste())
    expect(r.faturamentoMes).toBe(1500)
    expect(r.honorariosAReceber).toBe(3500 + 8200)
    expect(r.contasAPagar).toBe(2800 + 420)
    expect(r.contasAReceber).toBe(640)
    expect(r.saldo).toBe(1500 + 2000)
  })

  it('só conta como faturamento do mês o honorário pago no mês corrente', () => {
    const daquiAUmAno = new Date(new Date().getFullYear() + 1, 0, 15)

    expect(calcularResumo(lancamentosDeTeste(), daquiAUmAno).faturamentoMes).toBe(0)
  })
})

describe('filtrarLancamentos', () => {
  it('filtra por status', () => {
    const atrasados = filtrarLancamentos(lancamentosDeTeste(), {
      status: 'Atrasado',
      periodo: 'todos',
    })

    expect(atrasados.map((l) => l.id).sort()).toEqual(['2', '5'])
  })

  it('"próximos 30 dias" exclui o que já venceu', () => {
    const proximos = filtrarLancamentos(lancamentosDeTeste(), {
      status: null,
      periodo: 'proximos30',
    })

    expect(proximos.every((l) => l.status === 'Pendente')).toBe(true)
    expect(proximos).toHaveLength(3)
  })
})

describe('fluxoDeCaixa', () => {
  it('traz seis meses terminando no mês atual', () => {
    const fluxo = fluxoDeCaixa([], new Date(2026, 10, 4))

    expect(fluxo.map((m) => m.mes)).toEqual(['Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov'])
  })
})

import { calcularResumo, lancamentosMock } from './mocks'

describe('calcularResumo', () => {
  it('separa o que está em aberto do que foi pago', () => {
    const r = calcularResumo(lancamentosMock)
    expect(r.honorariosAReceber).toBe(3500 + 8200)
    expect(r.honorariosRecebidosMes).toBe(1500)
    expect(r.contasAPagar).toBe(2800 + 420)
    expect(r.contasAReceber).toBe(640)
    expect(r.saldo).toBe(1500 + 2000)
  })
})

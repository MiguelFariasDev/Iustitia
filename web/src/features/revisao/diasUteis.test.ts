import {
  addBusinessDays,
  isDiaUtil,
  isFeriadoNacional,
  isRecesso,
  pascoa,
  vencimentoPorDisponibilizacao,
} from './diasUteis'

const data = (a: number, m: number, d: number) => new Date(a, m - 1, d)
const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

describe('addBusinessDays', () => {
  it('conta a partir do dia útil seguinte (exclui o dia do começo)', () => {
    // quinta 5/nov + 1 dia útil = sexta 6/nov
    expect(iso(addBusinessDays(data(2026, 11, 5), 1))).toBe('2026-11-06')
  })

  it('pula sábado e domingo', () => {
    // sexta 6/nov + 1 = segunda 9/nov
    expect(iso(addBusinessDays(data(2026, 11, 6), 1))).toBe('2026-11-09')
    // quinta 5/nov + 5 = quinta 12/nov
    expect(iso(addBusinessDays(data(2026, 11, 5), 5))).toBe('2026-11-12')
  })

  it('pula o feriado nacional de 20/11 (Consciência Negra, desde 2024)', () => {
    // 15 dias úteis a partir de segunda 16/11: a sexta 20/11 não conta → 8/12
    expect(iso(addBusinessDays(data(2026, 11, 16), 15))).toBe('2026-12-08')
    // 15 dias úteis a partir de quinta 5/nov: 3 semanas, mais 1 dia por causa do feriado
    expect(iso(addBusinessDays(data(2026, 11, 5), 15))).toBe('2026-11-27')
  })

  it('não conta a Sexta-feira da Paixão', () => {
    // quarta 1/abr/2026 + 3: 2/4, (3/4 paixão), 6/4, 7/4
    expect(iso(addBusinessDays(data(2026, 4, 1), 3))).toBe('2026-04-07')
  })

  it('parte de um sábado: o primeiro dia útil é a segunda', () => {
    expect(iso(addBusinessDays(data(2026, 11, 7), 1))).toBe('2026-11-09')
  })

  it('zero dias devolve o mesmo dia, sem a hora', () => {
    const resultado = addBusinessDays(new Date(2026, 10, 5, 17, 30), 0)

    expect(iso(resultado)).toBe('2026-11-05')
    expect(resultado.getHours()).toBe(0)
  })

  it('suspende a contagem no recesso forense (20/12 a 20/01)', () => {
    // quinta 10/12 + 10: 6 antes do recesso (11 a 18/12) e 4 depois (21, 22, 25 e 26/01)
    expect(iso(addBusinessDays(data(2026, 12, 10), 10))).toBe('2027-01-26')
    // partindo de dentro do recesso: o dia 1 é o primeiro dia útil depois de 20/01
    expect(iso(addBusinessDays(data(2026, 12, 30), 3))).toBe('2027-01-25')
  })

  it('valor negativo ou fracionado não quebra: trata como zero ou trunca', () => {
    expect(iso(addBusinessDays(data(2026, 11, 5), -3))).toBe('2026-11-05')
    expect(iso(addBusinessDays(data(2026, 11, 5), 1.9))).toBe('2026-11-06')
  })

  it('não altera a data recebida', () => {
    const original = data(2026, 11, 5)
    addBusinessDays(original, 10)

    expect(iso(original)).toBe('2026-11-05')
  })
})

describe('calendário forense nacional', () => {
  it('calcula a Páscoa', () => {
    expect(iso(pascoa(2025))).toBe('2025-04-20')
    expect(iso(pascoa(2026))).toBe('2026-04-05')
  })

  it('reconhece feriados nacionais, recesso e dia útil', () => {
    expect(isFeriadoNacional(data(2026, 4, 3))).toBe(true)
    expect(isFeriadoNacional(data(2026, 11, 20))).toBe(true)
    expect(isFeriadoNacional(data(2023, 11, 20))).toBe(false)
    expect(isRecesso(data(2026, 12, 20))).toBe(true)
    expect(isRecesso(data(2027, 1, 21))).toBe(false)
    expect(isDiaUtil(data(2026, 11, 23))).toBe(true)
    expect(isDiaUtil(data(2026, 11, 21))).toBe(false)
  })
})

describe('vencimentoPorDisponibilizacao (mesma regra do servidor)', () => {
  it('disponibilização em feriado (20/11/2026, sexta): publica na segunda 23/11 e 15 dias úteis vencem em 14/12', () => {
    const data = vencimentoPorDisponibilizacao('2026-11-20T15:00:00Z', 15)

    expect([data.getFullYear(), data.getMonth() + 1, data.getDate()]).toEqual([2026, 12, 14])
  })

  it('5 dias úteis da mesma publicação vencem em 30/11', () => {
    const data = vencimentoPorDisponibilizacao('2026-11-20T15:00:00Z', 5)

    expect([data.getMonth() + 1, data.getDate()]).toEqual([11, 30])
  })

  it('usa a data de Brasília: 01/12 01:30 UTC ainda é 30/11 à noite', () => {
    // 30/11 (segunda) → publicação 01/12 (terça) → 1 dia útil: 02/12.
    const data = vencimentoPorDisponibilizacao('2026-12-01T01:30:00Z', 1)

    expect([data.getMonth() + 1, data.getDate()]).toEqual([12, 2])
  })
})

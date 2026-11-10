import { addBusinessDays } from './diasUteis'

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

  it('15 dias úteis são 3 semanas exatas quando se parte de um dia útil', () => {
    expect(iso(addBusinessDays(data(2026, 11, 5), 15))).toBe('2026-11-26')
  })

  it('parte de um sábado: o primeiro dia útil é a segunda', () => {
    expect(iso(addBusinessDays(data(2026, 11, 7), 1))).toBe('2026-11-09')
  })

  it('zero dias devolve o mesmo dia, sem a hora', () => {
    const resultado = addBusinessDays(new Date(2026, 10, 5, 17, 30), 0)

    expect(iso(resultado)).toBe('2026-11-05')
    expect(resultado.getHours()).toBe(0)
  })

  it('atravessa a virada de ano', () => {
    expect(iso(addBusinessDays(data(2026, 12, 30), 3))).toBe('2027-01-04')
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

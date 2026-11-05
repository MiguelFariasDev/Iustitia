import { dataPorExtenso, primeiroNome, saudacao } from './time'
import { personaFor } from './persona'

describe('saudacao', () => {
  it.each([
    [0, 'Bom dia'],
    [11, 'Bom dia'],
    [12, 'Boa tarde'],
    [17, 'Boa tarde'],
    [18, 'Boa noite'],
    [23, 'Boa noite'],
  ])('às %ih diz "%s"', (hora, esperado) => {
    expect(saudacao(new Date(2026, 10, 5, hora))).toBe(esperado)
  })
})

describe('dataPorExtenso', () => {
  it('dia da semana com inicial maiúscula, dia e mês, sem ano', () => {
    expect(dataPorExtenso(new Date(2026, 10, 5))).toBe('Quinta-feira, 5 de novembro')
  })
})

describe('primeiroNome', () => {
  it.each([
    ['Ana Souza', 'Ana'],
    ['Dra. Ana Souza', 'Ana'],
    ['  Dr. João Pereira ', 'João'],
    ['Maria', 'Maria'],
    ['', ''],
    [null, ''],
  ])('"%s" → "%s"', (entrada, esperado) => {
    expect(primeiroNome(entrada)).toBe(esperado)
  })
})

describe('personaFor', () => {
  it('mapeia os papéis do backend nas quatro visões do painel', () => {
    expect(personaFor('Owner')).toBe('socio')
    expect(personaFor('Partner')).toBe('socio')
    expect(personaFor('Lawyer')).toBe('advogado')
    expect(personaFor('Admin')).toBe('secretaria')
    expect(personaFor('Intern')).toBe('secretaria')
    expect(personaFor('Financial')).toBe('financeiro')
  })

  it('advogado autônomo vê o painel de advogado, mesmo sendo Owner', () => {
    expect(personaFor('Owner', 'Solo')).toBe('advogado')
    expect(personaFor('Owner', 'Firm')).toBe('socio')
  })

  it('papel desconhecido cai na visão de menor privilégio', () => {
    expect(personaFor(null)).toBe('secretaria')
    expect(personaFor(undefined)).toBe('secretaria')
  })
})

import { formatCnpj, isValidCnpj } from './cnpj'

describe('CNPJ', () => {
  it('valida dígitos verificadores', () => {
    expect(isValidCnpj('11.222.333/0001-81')).toBe(true)
    expect(isValidCnpj('11222333000181')).toBe(true)
    expect(isValidCnpj('11.222.333/0001-82')).toBe(false)
    expect(isValidCnpj('11.111.111/1111-11')).toBe(false)
    expect(isValidCnpj('123')).toBe(false)
  })

  it('formata progressivamente', () => {
    expect(formatCnpj('11222333000181')).toBe('11.222.333/0001-81')
    expect(formatCnpj('11222')).toBe('11.222')
  })
})

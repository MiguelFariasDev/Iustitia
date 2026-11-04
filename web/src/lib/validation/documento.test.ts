import {
  formatCep,
  formatCnj,
  formatCpf,
  formatTelefone,
  isValidCnj,
  isValidCpf,
  isValidTelefone,
} from './documento'

describe('CPF', () => {
  it.each(['111.444.777-35', '11144477735', '529.982.247-25'])('aceita %s', (value) => {
    expect(isValidCpf(value)).toBe(true)
  })

  it.each(['111.444.777-36', '123', '', '1114447773'])('rejeita %s', (value) => {
    expect(isValidCpf(value)).toBe(false)
  })

  it.each(['000.000.000-00', '111.111.111-11', '99999999999'])(
    'rejeita sequência de dígitos iguais: %s',
    (value) => {
      // Passa na aritmética dos verificadores mas não é documento de ninguém.
      expect(isValidCpf(value)).toBe(false)
    }
  )

  it('aplica a máscara enquanto se digita', () => {
    expect(formatCpf('111')).toBe('111')
    expect(formatCpf('111444')).toBe('111.444')
    expect(formatCpf('11144477735')).toBe('111.444.777-35')
  })

  it('descarta dígitos além do tamanho do CPF', () => {
    expect(formatCpf('111444777359999')).toBe('111.444.777-35')
  })
})

describe('número CNJ', () => {
  // Dígitos verificadores corretos (mod 97, ISO 7064).
  const valido = '1234567-48.2026.8.26.0001'

  it('aceita com máscara e sem máscara', () => {
    expect(isValidCnj(valido)).toBe(true)
    expect(isValidCnj('12345674820268260001')).toBe(true)
  })

  it('rejeita dígito verificador errado', () => {
    // Mesmo número com DD trocado de 48 para 49 — é o erro de digitação típico, e aceitá-lo
    // faria a captura de publicações buscar um processo que não existe.
    expect(isValidCnj('1234567-49.2026.8.26.0001')).toBe(false)
  })

  it('rejeita segmento 0, que não existe na Resolução CNJ 65/2008', () => {
    expect(isValidCnj('1234567-48.2026.0.26.0001')).toBe(false)
  })

  it.each(['', '123', '123456789012345678901'])('rejeita tamanho inválido: %s', (value) => {
    expect(isValidCnj(value)).toBe(false)
  })

  it('aplica a máscara progressivamente', () => {
    expect(formatCnj('1234567')).toBe('1234567')
    expect(formatCnj('123456748')).toBe('1234567-48')
    expect(formatCnj('12345674820268260001')).toBe(valido)
  })
})

describe('telefone', () => {
  it.each(['(11) 98765-4321', '1198765432'])('aceita %s', (value) => {
    expect(isValidTelefone(value)).toBe(true)
  })

  it.each(['119876543', '', '119876543210'])('rejeita %s', (value) => {
    expect(isValidTelefone(value)).toBe(false)
  })

  it('formata fixo e celular de forma diferente', () => {
    expect(formatTelefone('1133334444')).toBe('(11) 3333-4444')
    expect(formatTelefone('11987654321')).toBe('(11) 98765-4321')
  })
})

describe('CEP', () => {
  it('aplica a máscara', () => {
    expect(formatCep('01310100')).toBe('01310-100')
  })
})

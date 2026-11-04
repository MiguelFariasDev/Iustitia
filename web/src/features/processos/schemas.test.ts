import { parseValor, processoFormSchema, processoFormDefaults } from './schemas'

const CLIENTE_ID = '11111111-1111-4111-8111-111111111111'

function values(overrides: Partial<typeof processoFormDefaults> = {}) {
  return {
    ...processoFormDefaults,
    cnjNumber: '1234567-48.2026.8.26.0001',
    clienteId: CLIENTE_ID,
    ...overrides,
  }
}

describe('validação do formulário de processo', () => {
  it('aceita o mínimo obrigatório', () => {
    expect(processoFormSchema.safeParse(values()).success).toBe(true)
  })

  it('rejeita número CNJ com dígito verificador errado', () => {
    const result = processoFormSchema.safeParse(values({ cnjNumber: '1234567-49.2026.8.26.0001' }))

    expect(result.success).toBe(false)
  })

  it('rejeita cliente não selecionado', () => {
    expect(processoFormSchema.safeParse(values({ clienteId: '' })).success).toBe(false)
  })

  it('aceita valor da causa vazio — é opcional', () => {
    expect(processoFormSchema.safeParse(values({ valorCausa: '' })).success).toBe(true)
  })

  it('rejeita valor da causa negativo', () => {
    expect(processoFormSchema.safeParse(values({ valorCausa: '-10' })).success).toBe(false)
  })
})

describe('parseValor', () => {
  it('interpreta o formato pt-BR', () => {
    // Number("1.234,56") é NaN e Number("1.234") daria 1,234 — um valor de causa mil vezes
    // menor, erro que só apareceria num relatório financeiro.
    expect(parseValor('1.234,56')).toBe(1234.56)
    expect(parseValor('15.000,00')).toBe(15000)
    expect(parseValor('1.234')).toBe(1234)
  })

  it('aceita número simples', () => {
    expect(parseValor('500')).toBe(500)
    expect(parseValor('500,50')).toBe(500.5)
  })
})

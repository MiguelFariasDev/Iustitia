import { clienteFormDefaults, clienteFormSchema } from './schemas'

function values(overrides: Partial<typeof clienteFormDefaults> = {}) {
  return { ...clienteFormDefaults, nome: 'Maria Souza', documento: '111.444.777-35', ...overrides }
}

describe('validação do formulário de cliente', () => {
  it('aceita pessoa física com CPF válido', () => {
    expect(clienteFormSchema.safeParse(values()).success).toBe(true)
  })

  it('aceita pessoa jurídica com CNPJ válido', () => {
    const result = clienteFormSchema.safeParse(
      values({ tipo: 'PessoaJuridica', documento: '11.222.333/0001-81' })
    )

    expect(result.success).toBe(true)
  })

  it('rejeita CPF em pessoa jurídica', () => {
    // Um refine genérico de "documento válido" aceitaria isso — daí a validação ser por tipo.
    const result = clienteFormSchema.safeParse(
      values({ tipo: 'PessoaJuridica', documento: '111.444.777-35' })
    )

    expect(result.success).toBe(false)
  })

  it('rejeita CNPJ em pessoa física', () => {
    const result = clienteFormSchema.safeParse(values({ documento: '11.222.333/0001-81' }))

    expect(result.success).toBe(false)
  })

  it('rejeita nome vazio', () => {
    expect(clienteFormSchema.safeParse(values({ nome: '' })).success).toBe(false)
  })

  it('aceita contato e endereço vazios — são opcionais', () => {
    const result = clienteFormSchema.safeParse(values({ email: '', telefone: '', cep: '' }))

    expect(result.success).toBe(true)
  })

  it('rejeita telefone incompleto', () => {
    expect(clienteFormSchema.safeParse(values({ telefone: '(11) 9876' })).success).toBe(false)
  })

  it('rejeita e-mail malformado', () => {
    expect(clienteFormSchema.safeParse(values({ email: 'nao-e-email' })).success).toBe(false)
  })

  it('rejeita data de nascimento no futuro', () => {
    const amanha = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10)

    expect(clienteFormSchema.safeParse(values({ dataNascimento: amanha })).success).toBe(false)
  })

  it('rejeita data de nascimento em pessoa jurídica', () => {
    const result = clienteFormSchema.safeParse(
      values({
        tipo: 'PessoaJuridica',
        documento: '11.222.333/0001-81',
        dataNascimento: '1990-05-10',
      })
    )

    expect(result.success).toBe(false)
  })

  it('aceita data de nascimento no passado em pessoa física', () => {
    expect(clienteFormSchema.safeParse(values({ dataNascimento: '1990-05-10' })).success).toBe(true)
  })
})

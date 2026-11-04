import { formatCpfOuCnpj, isDocumentoContatoValido } from './documento'
import { contatoFormDefaults, contatoFormSchema } from './schemas'
import { mockListContatos } from './mocks'

const valido = { ...contatoFormDefaults, nome: 'Carlos Pereira' }

describe('contatoFormSchema', () => {
  it('só o nome é obrigatório', () => {
    expect(contatoFormSchema.safeParse(valido).success).toBe(true)
  })

  it('recusa nome em branco', () => {
    const r = contatoFormSchema.safeParse({ ...valido, nome: '   ' })

    expect(r.success).toBe(false)
    expect(r.error?.issues[0]?.message).toBe('Informe o nome do contato.')
  })

  it('aceita CPF e CNPJ válidos e recusa dígito verificador errado', () => {
    expect(contatoFormSchema.safeParse({ ...valido, documento: '529.982.247-25' }).success).toBe(
      true
    )
    expect(
      contatoFormSchema.safeParse({ ...valido, documento: '11.222.333/0001-81' }).success
    ).toBe(true)
    expect(contatoFormSchema.safeParse({ ...valido, documento: '529.982.247-26' }).success).toBe(
      false
    )
  })

  it('recusa documento com tamanho que não é de CPF nem de CNPJ', () => {
    expect(isDocumentoContatoValido('1234567')).toBe(false)
  })

  it('valida e-mail e telefone só quando preenchidos', () => {
    expect(contatoFormSchema.safeParse({ ...valido, email: 'sem-arroba' }).success).toBe(false)
    expect(contatoFormSchema.safeParse({ ...valido, telefone: '123' }).success).toBe(false)
    expect(
      contatoFormSchema.safeParse({ ...valido, email: 'a@b.com', telefone: '(11) 98765-4321' })
        .success
    ).toBe(true)
  })

  it('todos os contatos de exemplo respeitam as próprias regras', () => {
    for (const c of mockListContatos()) {
      const r = contatoFormSchema.safeParse({
        nome: c.nome,
        tipo: c.tipo,
        documento: c.documento ?? '',
        email: c.email ?? '',
        telefone: c.telefone ?? '',
        observacoes: c.observacoes ?? '',
      })
      expect(r.success, c.nome).toBe(true)
    }
  })
})

describe('formatCpfOuCnpj', () => {
  it('usa máscara de CPF até 11 dígitos e troca para CNPJ depois', () => {
    expect(formatCpfOuCnpj('52998224725')).toBe('529.982.247-25')
    expect(formatCpfOuCnpj('112223330001')).toBe('11.222.333/0001')
    expect(formatCpfOuCnpj('11222333000181')).toBe('11.222.333/0001-81')
  })

  it('ignora o que não é dígito e corta no tamanho do CNPJ', () => {
    expect(formatCpfOuCnpj('abc')).toBe('')
    expect(formatCpfOuCnpj('112223330001819999')).toBe('11.222.333/0001-81')
  })
})

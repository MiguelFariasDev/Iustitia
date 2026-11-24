import { onboardingOfficeSchema } from './schemas'

const base = { officeName: '', cnpj: '', cpf: '', oabState: 'CE', phone: '' }
const CPF_VALIDO = '529.982.247-25'

function erros(values: Record<string, unknown>) {
  const result = onboardingOfficeSchema.safeParse(values)

  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join('.')}: ${issue.message}`)
}

describe('onboardingOfficeSchema — escritório (Firm)', () => {
  it('exige o nome do escritório', () => {
    expect(erros({ ...base, accountType: 'Firm' })).toContain(
      'officeName: Informe o nome do escritório.'
    )
  })

  it('exige o CNPJ e que ele seja válido', () => {
    expect(erros({ ...base, accountType: 'Firm', officeName: 'Souza & Associados' })).toEqual([
      'cnpj: Informe o CNPJ do escritório.',
    ])
    expect(
      erros({
        ...base,
        accountType: 'Firm',
        officeName: 'Souza & Associados',
        cnpj: '11.111.111/1111-11',
      })
    ).toEqual(['cnpj: CNPJ inválido.'])
  })

  it('ignora um CPF que sobrou de quando a pessoa tinha escolhido Solo', () => {
    expect(
      erros({
        ...base,
        accountType: 'Firm',
        officeName: 'Souza & Associados',
        cnpj: '11.222.333/0001-81',
        cpf: '123',
      })
    ).toEqual([])
  })
})

describe('onboardingOfficeSchema — advogado autônomo (Solo)', () => {
  it('exige o CPF e não pede nome de escritório', () => {
    expect(erros({ ...base, accountType: 'Solo' })).toEqual(['cpf: Informe o seu CPF.'])
  })

  it('recusa CPF com dígito verificador errado', () => {
    expect(erros({ ...base, accountType: 'Solo', cpf: '111.111.111-11' })).toEqual([
      'cpf: CPF inválido.',
    ])
  })

  it('aceita CPF válido sem nome de escritório nem CNPJ', () => {
    expect(erros({ ...base, accountType: 'Solo', cpf: CPF_VALIDO })).toEqual([])
  })

  it('ignora um CNPJ inválido que sobrou de quando a pessoa tinha escolhido Firm', () => {
    expect(erros({ ...base, accountType: 'Solo', cpf: CPF_VALIDO, cnpj: '123' })).toEqual([])
  })

  it('recusa tipo de conta desconhecido', () => {
    expect(erros({ ...base, accountType: 'Outro', cpf: CPF_VALIDO }).length).toBeGreaterThan(0)
  })
})

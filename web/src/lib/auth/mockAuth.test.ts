import { MOCK_PASSWORD, mockAuthService, registerMockAccount, resetMockAccounts } from './mockAuth'

afterEach(() => resetMockAccounts())

describe('mockAuthService — contas cadastradas na sessão', () => {
  it('conta cadastrada entra com a senha que escolheu, com o nome e o tipo de conta dela', async () => {
    registerMockAccount({
      email: 'Joana@Exemplo.com',
      password: 'MinhaSenha1!',
      name: 'Joana Lima',
      accountType: 'Solo',
    })

    const session = await mockAuthService.signIn({
      email: 'joana@exemplo.com',
      password: 'MinhaSenha1!',
    })

    expect(session.user.name).toBe('Joana Lima')
    expect(session.user.accountType).toBe('Solo')
  })

  it('conta cadastrada NÃO aceita a senha padrão do mock', async () => {
    registerMockAccount({
      email: 'joana@exemplo.com',
      password: 'MinhaSenha1!',
      name: 'Joana Lima',
      accountType: 'Solo',
    })

    await expect(
      mockAuthService.signIn({ email: 'joana@exemplo.com', password: MOCK_PASSWORD })
    ).rejects.toThrow('E-mail ou senha inválidos.')
  })

  it('e-mail não cadastrado continua entrando com a senha padrão, como antes', async () => {
    const session = await mockAuthService.signIn({
      email: 'qualquer@exemplo.com',
      password: MOCK_PASSWORD,
    })

    expect(session.user.accountType).toBe('Firm')
  })

  it('não existe mais signUp no navegador: o cadastro é do backend', () => {
    expect('signUp' in mockAuthService).toBe(false)
  })
})

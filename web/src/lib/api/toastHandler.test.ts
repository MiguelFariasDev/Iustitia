import { ApiError } from './errors'
import { ErrorCode } from './errorCodes'
import { handleApiError } from './toastHandler'
import { notify } from '@/lib/notify'

vi.mock('@/lib/notify', () => ({
  notify: { error: vi.fn(), success: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))

const notifyError = vi.mocked(notify.error)

beforeEach(() => notifyError.mockClear())

describe('handleApiError', () => {
  it('emite toast para erro genérico do servidor', () => {
    const handled = handleApiError(new ApiError(500, { detail: 'Falha interna.' }))

    expect(handled.presentation).toBe('toast')
    expect(notifyError).toHaveBeenCalledWith('Falha interna.')
  })

  it('NÃO emite toast para erro de formulário — pertence ao campo', () => {
    // Um toast sobre "e-mail já cadastrado" empurra a atenção para longe do input errado.
    const handled = handleApiError(
      new ApiError(409, { errorCode: ErrorCode.USER_EMAIL_DUPLICATED, detail: 'Já existe.' })
    )

    expect(handled.presentation).toBe('inline')
    expect(notifyError).not.toHaveBeenCalled()
  })

  it('NÃO emite toast quando a sessão acabou — o redirecionamento já cuida disso', () => {
    const handled = handleApiError(
      new ApiError(401, { errorCode: ErrorCode.AUTH_TOKEN_EXPIRED, detail: 'Expirou.' })
    )

    expect(handled.presentation).toBe('signOut')
    expect(notifyError).not.toHaveBeenCalled()
  })

  it('silencia "publicação já lida" — acontece com dois cliques, não é erro do usuário', () => {
    const handled = handleApiError(
      new ApiError(409, { errorCode: ErrorCode.PUBLICATION_ALREADY_READ, detail: 'Já lida.' })
    )

    expect(handled.presentation).toBe('silent')
    expect(notifyError).not.toHaveBeenCalled()
  })

  it('sobrescreve a mensagem do backend quando a UI tem mais contexto', () => {
    const handled = handleApiError(
      new ApiError(401, {
        errorCode: ErrorCode.AUTH_INVALID_CREDENTIALS,
        detail: 'E-mail ou senha inválidos.',
      })
    )

    expect(handled.message).toBe('E-mail ou senha incorretos.')
  })

  it('usa a mensagem do backend quando não há sobrescrita — sem duplicar o catálogo', () => {
    const handled = handleApiError(
      new ApiError(409, { errorCode: 'TENANT_SUSPENDED', detail: 'O escritório está suspenso.' })
    )

    expect(handled.message).toBe('O escritório está suspenso.')
  })

  it('trata 401 sem código conhecido como fim de sessão', () => {
    expect(handleApiError(new ApiError(401)).presentation).toBe('signOut')
  })

  it('preserva o correlationId para suporte', () => {
    const handled = handleApiError(new ApiError(500, { correlationId: 'abc-123' }))

    expect(handled.correlationId).toBe('abc-123')
  })

  it('expõe os erros por campo do FluentValidation', () => {
    const handled = handleApiError(
      new ApiError(400, { errors: { Email: ['E-mail obrigatório.'] } })
    )

    expect(handled.fieldErrors.Email).toEqual(['E-mail obrigatório.'])
  })

  it('normaliza erro que não é ApiError', () => {
    const handled = handleApiError(new Error('boom'))

    expect(handled.code).toBe('UNKNOWN')
    expect(handled.message).toBe('boom')
  })
})

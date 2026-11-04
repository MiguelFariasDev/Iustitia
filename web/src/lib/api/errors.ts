import type { ProblemDetails } from './types'

const fallbackMessages: Record<number, string> = {
  0: 'Não foi possível conectar ao servidor. Verifique sua conexão.',
  400: 'Os dados enviados são inválidos.',
  401: 'Sua sessão expirou. Entre novamente.',
  403: 'Você não tem permissão para esta ação.',
  404: 'Recurso não encontrado.',
  408: 'O servidor demorou para responder. Tente novamente.',
  409: 'Conflito: o recurso foi alterado por outra pessoa.',
  429: 'Muitas requisições. Aguarde um instante.',
  500: 'Erro inesperado no servidor. Nossa equipe foi notificada.',
}

/** Erro normalizado de toda chamada à API: a UI nunca lida com AxiosError diretamente. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId: string | undefined
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails = {}) {
    super(problem.detail || fallbackMessages[status] || fallbackMessages[500])
    this.name = 'ApiError'
    this.status = status
    this.code = problem.errorCode ?? problem.title ?? `HTTP_${status}`
    this.correlationId = problem.correlationId
    this.fieldErrors = problem.errors ?? {}
  }

  get isNetworkError() {
    return this.status === 0
  }

  get isUnauthorized() {
    return this.status === 401
  }
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    ('title' in value || 'detail' in value || 'errorCode' in value)
  )
}

/** Mensagem amigável para qualquer erro (toasts, Error Boundary). */
export function getErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message) return error.message
  return 'Algo deu errado. Tente novamente.'
}

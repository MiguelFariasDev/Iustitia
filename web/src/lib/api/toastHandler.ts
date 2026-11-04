import { ApiError } from './errors'
import { getErrorCodeMessage, getErrorPresentation, type ErrorPresentation } from './errorCodes'
import { notify } from '@/lib/notify'

export interface HandledError {
  message: string
  code: string
  presentation: ErrorPresentation
  fieldErrors: Record<string, string[]>
  correlationId: string | undefined
}

/**
 * Normaliza qualquer erro em algo que a UI sabe exibir e decide se merece toast.
 *
 * Emite o toast apenas para `presentation === 'toast'`: erro de formulário pertence ao
 * campo (um toast sobre "e-mail já cadastrado" empurra a atenção para longe do input que
 * precisa ser corrigido), e erro de sessão já dispara redirecionamento no interceptor —
 * um toast que some junto com a página só pisca.
 */
export function handleApiError(error: unknown, options: { silent?: boolean } = {}): HandledError {
  const apiError = error instanceof ApiError ? error : null
  const status = apiError?.status ?? 500
  const code = apiError?.code ?? 'UNKNOWN'

  const message = getErrorCodeMessage(
    apiError?.code,
    error instanceof Error && error.message ? error.message : 'Algo deu errado. Tente novamente.'
  )

  const presentation = getErrorPresentation(apiError?.code, status)

  if (!options.silent && presentation === 'toast') {
    notify.error(message)
  }

  return {
    message,
    code,
    presentation,
    fieldErrors: apiError?.fieldErrors ?? {},
    correlationId: apiError?.correlationId,
  }
}

/**
 * Códigos de erro do catálogo do backend (BuildingBlocks.Domain.Errors.ErrorCode).
 *
 * Só estão listados os códigos em que a UI PRECISA agir de forma diferente — realçar um
 * campo, oferecer uma ação, mudar o tom. Para todo o resto, a mensagem amigável já vem em
 * `detail` do ProblemDetails, em pt-BR, escrita uma única vez no backend
 * (ver docs/errors/catalog.md).
 *
 * Espelhar as ~80 mensagens aqui criaria duas fontes de verdade que divergem na primeira
 * correção de texto feita só de um lado.
 */
export const ErrorCode = {
  // Sessão / autenticação
  AUTH_INVALID_CREDENTIALS: 'AUTH_INVALID_CREDENTIALS',
  AUTH_TOKEN_EXPIRED: 'AUTH_TOKEN_EXPIRED',
  AUTH_TOKEN_INVALID: 'AUTH_TOKEN_INVALID',
  AUTH_REFRESH_TOKEN_EXPIRED: 'AUTH_REFRESH_TOKEN_EXPIRED',
  AUTH_REFRESH_TOKEN_INVALID: 'AUTH_REFRESH_TOKEN_INVALID',
  AUTH_EMAIL_NOT_CONFIRMED: 'AUTH_EMAIL_NOT_CONFIRMED',
  AUTH_ACCOUNT_DISABLED: 'AUTH_ACCOUNT_DISABLED',

  // Autorização
  AUTHORIZATION_FORBIDDEN: 'AUTHORIZATION_FORBIDDEN',
  AUTHORIZATION_INSUFFICIENT_ROLE: 'AUTHORIZATION_INSUFFICIENT_ROLE',
  AUTHORIZATION_TENANT_MISMATCH: 'AUTHORIZATION_TENANT_MISMATCH',

  // Validação
  VALIDATION_REQUIRED_FIELD: 'VALIDATION_REQUIRED_FIELD',
  VALIDATION_INVALID_FORMAT: 'VALIDATION_INVALID_FORMAT',
  VALIDATION_INVALID_ENUM: 'VALIDATION_INVALID_ENUM',

  // Usuários
  USER_EMAIL_DUPLICATED: 'USER_EMAIL_DUPLICATED',
  USER_NOT_FOUND: 'USER_NOT_FOUND',
  USER_CANNOT_DEACTIVATE_SELF: 'USER_CANNOT_DEACTIVATE_SELF',
  USER_CANNOT_REMOVE_LAST_OWNER: 'USER_CANNOT_REMOVE_LAST_OWNER',

  // Publicações (módulo Legal)
  PUBLICATION_NOT_FOUND: 'PUBLICATION_NOT_FOUND',
  PUBLICATION_ALREADY_READ: 'PUBLICATION_ALREADY_READ',
  PUBLICATION_INVALID_STATUS_TRANSITION: 'PUBLICATION_INVALID_STATUS_TRANSITION',

  // Alertas
  ALERT_NOT_FOUND: 'ALERT_NOT_FOUND',
  ALERT_ALREADY_READ: 'ALERT_ALREADY_READ',
  ALERT_ALREADY_DISMISSED: 'ALERT_ALREADY_DISMISSED',

  // Integração com o CNJ
  CNJ_UNAVAILABLE: 'CNJ_UNAVAILABLE',
  CNJ_RATE_LIMITED: 'CNJ_RATE_LIMITED',
} as const

export type ErrorCodeValue = (typeof ErrorCode)[keyof typeof ErrorCode]

/** Grupos do catálogo (BuildingBlocks.Domain.Errors.ErrorGroup). */
export const ErrorGroup = {
  Common: 'Common',
  Validation: 'Validation',
  Auth: 'Auth',
  Authorization: 'Authorization',
  Tenant: 'Tenant',
  User: 'User',
  Audit: 'Audit',
  Settings: 'Settings',
  FeatureFlags: 'FeatureFlags',
  Backup: 'Backup',
  Integration: 'Integration',
  Publication: 'Publication',
  Alert: 'Alert',
  Cnj: 'Cnj',
  Internal: 'Internal',
} as const

export type ErrorGroupValue = (typeof ErrorGroup)[keyof typeof ErrorGroup]

/**
 * Como a UI reage a um erro.
 * - `toast`: padrão, avisa e segue.
 * - `inline`: pertence a um formulário; quem chamou decide onde mostrar (não emite toast).
 * - `silent`: esperado no fluxo, não merece ruído (ex.: publicação já estava lida).
 * - `signOut`: a sessão acabou; encerrar e mandar para o login.
 */
export type ErrorPresentation = 'toast' | 'inline' | 'silent' | 'signOut'

const presentationByCode: Partial<Record<string, ErrorPresentation>> = {
  [ErrorCode.AUTH_INVALID_CREDENTIALS]: 'inline',
  [ErrorCode.AUTH_EMAIL_NOT_CONFIRMED]: 'inline',
  [ErrorCode.VALIDATION_REQUIRED_FIELD]: 'inline',
  [ErrorCode.VALIDATION_INVALID_FORMAT]: 'inline',
  [ErrorCode.USER_EMAIL_DUPLICATED]: 'inline',

  [ErrorCode.AUTH_TOKEN_EXPIRED]: 'signOut',
  [ErrorCode.AUTH_TOKEN_INVALID]: 'signOut',
  [ErrorCode.AUTH_REFRESH_TOKEN_EXPIRED]: 'signOut',
  [ErrorCode.AUTH_REFRESH_TOKEN_INVALID]: 'signOut',

  // Marcar como lida uma publicação já lida acontece com dois cliques ou duas abas.
  // É ruído, não erro que o usuário precise ver.
  [ErrorCode.PUBLICATION_ALREADY_READ]: 'silent',

  // Idem para alertas: dois cliques, duas abas, ou o alerta já foi tratado em outro lugar.
  [ErrorCode.ALERT_ALREADY_READ]: 'silent',
  [ErrorCode.ALERT_ALREADY_DISMISSED]: 'silent',
}

/**
 * Sobrescritas de mensagem. Só onde a UI tem contexto que o backend não tem — o backend não
 * sabe que o usuário está numa tela de login, por exemplo.
 */
const messageByCode: Partial<Record<string, string>> = {
  [ErrorCode.AUTH_INVALID_CREDENTIALS]: 'E-mail ou senha incorretos.',
  [ErrorCode.AUTHORIZATION_TENANT_MISMATCH]:
    'Sua sessão não está associada a um escritório. Entre novamente.',
  [ErrorCode.PUBLICATION_NOT_FOUND]:
    'Esta publicação não existe mais ou não pertence ao seu escritório.',
  [ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION]:
    'Esta publicação já foi revisada e não pode voltar para "não tratada". Atualize a página.',
  [ErrorCode.ALERT_NOT_FOUND]: 'Este alerta não existe mais.',
  [ErrorCode.CNJ_UNAVAILABLE]:
    'O serviço de publicações do CNJ está fora do ar. A captura será retomada automaticamente.',
  [ErrorCode.CNJ_RATE_LIMITED]:
    'Muitas consultas ao CNJ neste momento. A captura será retomada automaticamente.',
}

export function getErrorPresentation(code: string | undefined, status: number): ErrorPresentation {
  if (code && presentationByCode[code]) return presentationByCode[code]
  // 401 sem código conhecido também encerra a sessão: é o caso de um token recusado
  // antes de qualquer handler rodar.
  if (status === 401) return 'signOut'
  if (status === 400) return 'inline'

  return 'toast'
}

/** Mensagem a exibir: sobrescrita da UI quando existe, senão a do backend. */
export function getErrorCodeMessage(code: string | undefined, fallback: string): string {
  return (code && messageByCode[code]) || fallback
}

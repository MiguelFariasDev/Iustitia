import type { Route } from 'next'

export const routes = {
  home: '/',
  login: '/login',
  forgotPassword: '/esqueci-senha',
  resetPassword: '/redefinir-senha',
  onboarding: '/onboarding',
  dashboard: '/painel',
  publications: '/publicacoes',
  alerts: '/alertas',
  agenda: '/agenda',
  processes: '/processos',
  clients: '/clientes',
  contacts: '/contatos',
  serviceDesk: '/atendimentos',
  finance: '/financeiro',
  drafting: '/criacao-de-pecas',
  documents: '/documentos',
  indicators: '/indicadores',
  settings: {
    root: '/configuracoes',
    profile: '/configuracoes/perfil',
    tenant: '/configuracoes/escritorio',
    users: '/configuracoes/usuarios',
    notifications: '/configuracoes/notificacoes',
    integrations: '/configuracoes/integracoes',
    audit: '/configuracoes/auditoria',
  },
} as const satisfies Record<string, Route | Record<string, Route>>

/**
 * Rotas só para visitantes: um usuário autenticado é mandado para o painel. A landing (`/`) é
 * pública para visitantes; quem já tem sessão vai direto ao painel.
 */
export const guestOnlyPaths = ['/', '/login', '/esqueci-senha'] as const

/** Rotas públicas acessíveis com ou sem sessão (links de e-mail, cadastro). */
export const publicPaths = ['/redefinir-senha', '/convite', '/onboarding'] as const

export function isGuestOnlyPath(pathname: string) {
  // '/' só vale exato: como prefixo casaria o site inteiro.
  return guestOnlyPaths.some((p) => pathname === p || (p !== '/' && pathname.startsWith(`${p}/`)))
}

export function isPublicPath(pathname: string) {
  return (
    isGuestOnlyPath(pathname) ||
    publicPaths.some((p) => pathname === p || pathname.startsWith(`${p}/`))
  )
}

/** Evita open redirect: aceita apenas caminhos relativos internos. */
export function safeRedirectPath(
  next: string | null | undefined,
  fallback: string = routes.dashboard
) {
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.startsWith('/\\')) {
    return fallback
  }
  return next
}

import type { Route } from 'next'

export const routes = {
  home: '/',
  login: '/login',
  signup: '/cadastro',
  terms: '/termos',
  privacy: '/privacidade',
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

/** Rotas só para visitantes: um usuário autenticado é mandado para o painel. */
export const guestOnlyPaths = ['/login', '/esqueci-senha', '/cadastro'] as const

/**
 * Rotas públicas acessíveis com ou sem sessão (links de e-mail, páginas legais). `/` (landing) é
 * pública por igualdade exata — tratada à parte, porque como prefixo cobriria o site inteiro.
 * `/onboarding` NÃO é pública: exige sessão (é o guia do primeiro acesso, depois do cadastro).
 */
export const publicPaths = ['/redefinir-senha', '/convite', '/termos', '/privacidade'] as const

export function isGuestOnlyPath(pathname: string) {
  return guestOnlyPaths.some((p) => pathname === p || pathname.startsWith(`${p}/`))
}

export function isPublicPath(pathname: string) {
  return (
    pathname === '/' ||
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

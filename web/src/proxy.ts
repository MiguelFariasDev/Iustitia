import { NextResponse, type NextRequest } from 'next/server'
import { env, isMockAuth } from '@/config/env'
import { isGuestOnlyPath, isPublicPath, routes } from '@/config/routes'
import { MOCK_SESSION_COOKIE } from '@/lib/auth/mockAuth'
import { buildCsp, generateNonce } from '@/lib/security/csp'
import { updateSupabaseSession } from '@/lib/supabase/middleware'

/**
 * Proxy (antigo middleware — renomeado no Next.js 16): renova a sessão do Supabase e
 * faz o roteamento otimista por autenticação. NÃO é a fronteira de segurança: a API
 * .NET valida o JWT em toda requisição (ADR-003/029).
 */
export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl

  // CSP com nonce por requisição (ADR-070). O header precisa ir na REQUISIÇÃO para o Next.js
  // carimbar o nonce nos próprios scripts, e na RESPOSTA para o navegador aplicá-lo.
  const nonce = generateNonce()
  const csp = buildCsp({
    nonce,
    isDev: process.env.NODE_ENV !== 'production',
    apiUrl: env.apiUrl,
    supabaseUrl: env.supabaseUrl,
  })
  const requestHeaders = new Headers(request.headers)
  requestHeaders.set('x-nonce', nonce)
  requestHeaders.set('Content-Security-Policy', csp)

  let response: NextResponse
  let isAuthenticated: boolean

  if (isMockAuth) {
    response = NextResponse.next({ request: { headers: requestHeaders } })
    isAuthenticated = Boolean(request.cookies.get(MOCK_SESSION_COOKIE)?.value)
  } else {
    ;({ response, isAuthenticated } = await updateSupabaseSession(request, requestHeaders))
  }
  response.headers.set('Content-Security-Policy', csp)

  if (!isAuthenticated && !isPublicPath(pathname)) {
    const url = request.nextUrl.clone()
    url.pathname = routes.login
    url.search = pathname === '/' ? '' : `?next=${encodeURIComponent(pathname + search)}`
    return withCsp(NextResponse.redirect(url), csp)
  }

  // Landing é para visitantes; quem já tem sessão vai direto ao painel.
  if (isAuthenticated && (pathname === '/' || isGuestOnlyPath(pathname))) {
    const url = request.nextUrl.clone()
    url.pathname = routes.dashboard
    url.search = ''
    return withCsp(NextResponse.redirect(url), csp)
  }

  return response
}

function withCsp(response: NextResponse, csp: string) {
  response.headers.set('Content-Security-Policy', csp)
  return response
}

export const config = {
  // Ignora assets estáticos, imagens otimizadas e arquivos com extensão.
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico|icon.svg|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico|txt|xml)$).*)',
  ],
}

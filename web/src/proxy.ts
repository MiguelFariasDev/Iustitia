import { NextResponse, type NextRequest } from 'next/server'
import { isMockAuth } from '@/config/env'
import { isGuestOnlyPath, isPublicPath, routes } from '@/config/routes'
import { MOCK_SESSION_COOKIE } from '@/lib/auth/mockAuth'
import { updateSupabaseSession } from '@/lib/supabase/middleware'

/**
 * Proxy (antigo middleware — renomeado no Next.js 16): renova a sessão do Supabase e
 * faz o roteamento otimista por autenticação. NÃO é a fronteira de segurança: a API
 * .NET valida o JWT em toda requisição (ADR-003/029).
 */
export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl

  let response: NextResponse
  let isAuthenticated: boolean

  if (isMockAuth) {
    response = NextResponse.next({ request })
    isAuthenticated = Boolean(request.cookies.get(MOCK_SESSION_COOKIE)?.value)
  } else {
    ;({ response, isAuthenticated } = await updateSupabaseSession(request))
  }

  if (!isAuthenticated && !isPublicPath(pathname)) {
    const url = request.nextUrl.clone()
    url.pathname = routes.login
    url.search = pathname === '/' ? '' : `?next=${encodeURIComponent(pathname + search)}`
    return NextResponse.redirect(url)
  }

  if (isAuthenticated && isGuestOnlyPath(pathname)) {
    const url = request.nextUrl.clone()
    url.pathname = routes.dashboard
    url.search = ''
    return NextResponse.redirect(url)
  }

  return response
}

export const config = {
  // Ignora assets estáticos, imagens otimizadas e arquivos com extensão.
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico|icon.svg|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico|txt|xml)$).*)',
  ],
}

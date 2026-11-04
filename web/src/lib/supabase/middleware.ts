import { createServerClient } from '@supabase/ssr'
import { NextResponse, type NextRequest } from 'next/server'
import { env } from '@/config/env'

/**
 * Renova a sessão do Supabase (cookies) a cada requisição e informa se há usuário.
 * Usado pelo proxy (src/proxy.ts). `getClaims()` valida a assinatura do JWT —
 * nunca confiar em `getSession()` no servidor.
 */
export async function updateSupabaseSession(request: NextRequest) {
  let response = NextResponse.next({ request })

  if (!env.supabaseUrl || !env.supabaseAnonKey) {
    return { response, isAuthenticated: false }
  }

  const supabase = createServerClient(env.supabaseUrl, env.supabaseAnonKey, {
    cookies: {
      getAll: () => request.cookies.getAll(),
      setAll: (cookiesToSet) => {
        cookiesToSet.forEach(({ name, value }) => request.cookies.set(name, value))
        response = NextResponse.next({ request })
        cookiesToSet.forEach(({ name, value, options }) =>
          response.cookies.set(name, value, options)
        )
      },
    },
  })

  const { data } = await supabase.auth.getClaims()
  return { response, isAuthenticated: Boolean(data?.claims?.sub) }
}

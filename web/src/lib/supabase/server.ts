import 'server-only'
import { createServerClient } from '@supabase/ssr'
import { cookies } from 'next/headers'
import { env } from '@/config/env'

/**
 * Cliente Supabase para Server Components / Route Handlers. Um por requisição
 * (nunca compartilhar entre requisições: carrega os cookies do usuário atual).
 */
export async function createSupabaseServerClient() {
  const cookieStore = await cookies()
  return createServerClient(env.supabaseUrl, env.supabaseAnonKey, {
    cookies: {
      getAll: () => cookieStore.getAll(),
      setAll: (cookiesToSet) => {
        try {
          cookiesToSet.forEach(({ name, value, options }) => cookieStore.set(name, value, options))
        } catch {
          // Server Components não podem gravar cookies; o proxy já renova a sessão.
        }
      },
    },
  })
}

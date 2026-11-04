import { createBrowserClient } from '@supabase/ssr'
import type { SupabaseClient } from '@supabase/supabase-js'
import { env } from '@/config/env'

let browserClient: SupabaseClient | undefined

export function isSupabaseConfigured() {
  return Boolean(env.supabaseUrl && env.supabaseAnonKey)
}

/**
 * Cliente Supabase do navegador (singleton). Sessão persistida em cookies via @supabase/ssr,
 * para que o proxy (src/proxy.ts) e Server Components enxerguem o mesmo usuário.
 */
export function getSupabaseBrowserClient(): SupabaseClient {
  if (!isSupabaseConfigured()) {
    throw new Error(
      'Supabase não configurado: defina NEXT_PUBLIC_SUPABASE_URL e NEXT_PUBLIC_SUPABASE_ANON_KEY (ou use NEXT_PUBLIC_AUTH_MODE=mock em desenvolvimento).'
    )
  }
  browserClient ??= createBrowserClient(env.supabaseUrl, env.supabaseAnonKey)
  return browserClient
}

/**
 * Content-Security-Policy da aplicação (Etapa 3.9, ADR-070). Função pura, montada por requisição
 * no proxy (src/proxy.ts) porque o nonce muda a cada resposta.
 *
 * Por que NONCE e não `script-src 'self' 'unsafe-inline'`: 'unsafe-inline' em script-src anula
 * a CSP contra XSS (qualquer script injetado executa). Com nonce + 'strict-dynamic' só roda o
 * script que o servidor carimbou. O Next.js aplica o nonce aos próprios scripts quando o header
 * CSP está na REQUISIÇÃO (por isso o proxy o repassa), e o layout o aplica ao script de tema.
 *
 * style-src mantém 'unsafe-inline': Radix/Tailwind/next-font emitem estilos inline e CSP de
 * estilo com nonce não é suportada de ponta a ponta. O risco residual (injeção de CSS) é bem
 * menor que o de script e está registrado no ADR.
 */
export interface CspOptions {
  nonce: string
  isDev: boolean
  /** URL da API .NET (NEXT_PUBLIC_API_URL) — só a origem entra no connect-src. */
  apiUrl: string
  /** URL do projeto Supabase (NEXT_PUBLIC_SUPABASE_URL), se configurado. */
  supabaseUrl?: string
}

function originOf(url: string | undefined): string | null {
  if (!url) return null
  try {
    return new URL(url).origin
  } catch {
    return null
  }
}

export function generateNonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  return btoa(String.fromCharCode(...bytes))
}

export function buildCsp({ nonce, isDev, apiUrl, supabaseUrl }: CspOptions): string {
  const apiOrigin = originOf(apiUrl)
  const supabaseOrigin = originOf(supabaseUrl)

  const connect = [
    "'self'",
    apiOrigin,
    // Projeto configurado: só ele. Sem projeto (mock/dev): o curinga do domínio Supabase, que
    // cobre REST/Auth e o WebSocket do Realtime.
    supabaseOrigin ?? 'https://*.supabase.co',
    supabaseOrigin ? supabaseOrigin.replace(/^http/, 'ws') : 'wss://*.supabase.co',
    // HMR do `next dev`.
    isDev ? 'ws://localhost:*' : null,
  ].filter((value): value is string => Boolean(value))

  const directives: Record<string, string[]> = {
    'default-src': ["'self'"],
    // 'unsafe-eval' só em dev: o React usa eval para reconstruir stacks de depuração.
    'script-src': ["'self'", `'nonce-${nonce}'`, "'strict-dynamic'", ...(isDev ? ["'unsafe-eval'"] : [])],
    'style-src': ["'self'", "'unsafe-inline'"],
    'img-src': ["'self'", 'data:', 'blob:', 'https:'],
    'font-src': ["'self'"],
    'connect-src': connect,
    'object-src': ["'none'"],
    'base-uri': ["'self'"],
    'form-action': ["'self'"],
    // Ninguém embute o app em iframe (clickjacking) — equivale ao X-Frame-Options: DENY.
    'frame-ancestors': ["'none'"],
    'frame-src': ["'none'"],
  }

  const policy = Object.entries(directives)
    .map(([name, values]) => `${name} ${values.join(' ')}`)
    .join('; ')

  return isDev ? policy : `${policy}; upgrade-insecure-requests`
}

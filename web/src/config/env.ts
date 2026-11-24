/**
 * Variáveis públicas lidas uma única vez. Next.js inlina `process.env.NEXT_PUBLIC_*` em build,
 * por isso cada variável precisa ser referenciada literalmente (sem acesso dinâmico).
 */
export type AuthMode = 'supabase' | 'mock'

const authMode: AuthMode = process.env.NEXT_PUBLIC_AUTH_MODE === 'mock' ? 'mock' : 'supabase'

export const env = {
  appName: process.env.NEXT_PUBLIC_APP_NAME || 'Advocacia',
  apiUrl: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api/v1',
  supabaseUrl: process.env.NEXT_PUBLIC_SUPABASE_URL ?? '',
  supabaseAnonKey: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? '',
  authMode,
  /** Enquanto os endpoints de negócio não existem, as features consomem mocks locais. */
  useMocks: process.env.NEXT_PUBLIC_API_MOCKS !== 'false',
  /**
   * Cadastro de advogado autônomo (Solo). O backend aceita `accountType` e CPF em POST /tenants
   * (ADR-068), então a escolha "advogado autônomo × escritório" é a primeira pergunta do
   * cadastro por padrão. `NEXT_PUBLIC_SOLO_SIGNUP=false` desliga, para apontar o frontend a um
   * backend antigo que ainda não conhece o tipo de conta.
   */
  soloSignupEnabled: process.env.NEXT_PUBLIC_SOLO_SIGNUP !== 'false',
} as const

export const isMockAuth = env.authMode === 'mock'

/**
 * Atributos dos cookies de sessão do Supabase. `Secure` em produção (nunca trafega em HTTP) e
 * `SameSite=Lax` (não é enviado em requisições cross-site de escrita: mitiga CSRF).
 *
 * LIMITAÇÃO CONHECIDA — `HttpOnly` NÃO é possível aqui: o cliente de navegador do Supabase lê o
 * token no JavaScript para chamar a API .NET com `Authorization: Bearer`. Tornar o cookie HttpOnly
 * exige um BFF (route handlers do Next.js intermediando as chamadas à API). Está registrado como
 * evolução no ADR-070; enquanto isso, a defesa é a CSP com nonce (sem script injetado, o token não
 * é lido) e nenhum token em localStorage (verificado por teste).
 */
export const COOKIE_OPTIONS = {
  sameSite: 'lax' as const,
  secure: process.env.NODE_ENV === 'production',
  path: '/',
}

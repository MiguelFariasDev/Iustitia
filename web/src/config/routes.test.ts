import { isGuestOnlyPath, isPublicPath, safeRedirectPath } from './routes'

describe('rotas', () => {
  it('classifica rotas públicas e só-visitante', () => {
    expect(isGuestOnlyPath('/login')).toBe(true)
    expect(isPublicPath('/convite/abc')).toBe(true)
    expect(isPublicPath('/redefinir-senha')).toBe(true)
    expect(isPublicPath('/painel')).toBe(false)
    expect(isPublicPath('/loginx')).toBe(false)
  })

  it('safeRedirectPath bloqueia open redirect', () => {
    expect(safeRedirectPath('/configuracoes/perfil')).toBe('/configuracoes/perfil')
    expect(safeRedirectPath('https://evil.com')).toBe('/painel')
    expect(safeRedirectPath('//evil.com')).toBe('/painel')
    expect(safeRedirectPath('/\\evil.com')).toBe('/painel')
    expect(safeRedirectPath(null)).toBe('/painel')
  })
})

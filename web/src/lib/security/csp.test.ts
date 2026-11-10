import { describe, expect, it } from 'vitest'
import { buildCsp, generateNonce } from './csp'

const base = { nonce: 'abc123', isDev: false, apiUrl: 'https://api.iustitia.com.br/api/v1' }

describe('buildCsp', () => {
  it('exige nonce nos scripts e NUNCA permite unsafe-inline/unsafe-eval em produção', () => {
    const script = buildCsp(base)
      .split('; ')
      .find((d) => d.startsWith('script-src'))!
    expect(script).toContain("'nonce-abc123'")
    expect(script).toContain("'strict-dynamic'")
    expect(script).not.toContain('unsafe-inline')
    expect(script).not.toContain('unsafe-eval')
  })

  it('permite unsafe-eval apenas em desenvolvimento', () => {
    expect(buildCsp({ ...base, isDev: true })).toContain("'unsafe-eval'")
  })

  it('bloqueia iframes, plugins e base-uri alheia', () => {
    const csp = buildCsp(base)
    expect(csp).toContain("frame-ancestors 'none'")
    expect(csp).toContain("frame-src 'none'")
    expect(csp).toContain("object-src 'none'")
    expect(csp).toContain("base-uri 'self'")
    expect(csp).toContain("default-src 'self'")
  })

  it('limita connect-src à origem da API e ao Supabase configurado (http→ws no realtime)', () => {
    const csp = buildCsp({ ...base, supabaseUrl: 'https://abcd.supabase.co' })
    const connect = csp.split('; ').find((d) => d.startsWith('connect-src'))!
    expect(connect).toContain('https://api.iustitia.com.br')
    expect(connect).not.toContain('/api/v1')
    expect(connect).toContain('https://abcd.supabase.co')
    expect(connect).toContain('wss://abcd.supabase.co')
    expect(connect).not.toContain('*')
  })

  it('sem projeto Supabase configurado, cai no curinga do domínio Supabase', () => {
    expect(buildCsp(base)).toContain('https://*.supabase.co')
  })

  it('só força HTTPS (upgrade-insecure-requests) fora do desenvolvimento', () => {
    expect(buildCsp(base)).toContain('upgrade-insecure-requests')
    expect(buildCsp({ ...base, isDev: true })).not.toContain('upgrade-insecure-requests')
  })

  it('ignora URL de API inválida sem quebrar', () => {
    expect(() => buildCsp({ ...base, apiUrl: 'não é url' })).not.toThrow()
  })
})

describe('generateNonce', () => {
  it('gera valores base64 distintos a cada chamada', () => {
    const a = generateNonce()
    const b = generateNonce()
    expect(a).toMatch(/^[A-Za-z0-9+/]+=*$/)
    expect(a).not.toBe(b)
  })
})

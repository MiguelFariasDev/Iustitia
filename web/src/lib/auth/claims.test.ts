import { decodeAccessTokenClaims, parseUserRole } from './claims'

function fakeJwt(payload: object) {
  const encode = (o: object) =>
    btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${encode({ alg: 'HS256' })}.${encode(payload)}.assinatura`
}

describe('claims do JWT (ADR-029)', () => {
  it('lê tenant_id e user_role emitidos pelo Custom Access Token Hook', () => {
    const claims = decodeAccessTokenClaims(
      fakeJwt({ sub: 'u1', tenant_id: 't1', user_role: 'Partner', nome: 'João' })
    )
    expect(claims).toMatchObject({ sub: 'u1', tenant_id: 't1', user_role: 'Partner' })
  })

  it('token malformado retorna objeto vazio (sem lançar)', () => {
    expect(decodeAccessTokenClaims('lixo')).toEqual({})
    expect(decodeAccessTokenClaims('a.@@@.c')).toEqual({})
  })

  it('parseUserRole aceita só papéis conhecidos', () => {
    expect(parseUserRole('Lawyer')).toBe('Lawyer')
    expect(parseUserRole('authenticated')).toBeNull()
    expect(parseUserRole(undefined)).toBeNull()
  })
})

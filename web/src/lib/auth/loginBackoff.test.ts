import { beforeEach, describe, expect, it } from 'vitest'
import {
  backoffSeconds,
  clearLoginFailures,
  recordLoginFailure,
  remainingLockSeconds,
} from './loginBackoff'

describe('loginBackoff', () => {
  beforeEach(() => sessionStorage.clear())

  it('não atrasa nas 4 primeiras falhas e começa a atrasar na 5ª', () => {
    expect([1, 2, 3, 4].map(backoffSeconds)).toEqual([0, 0, 0, 0])
    expect(backoffSeconds(5)).toBe(30)
    expect(backoffSeconds(6)).toBe(60)
    expect(backoffSeconds(7)).toBe(120)
  })

  it('tem teto de 15 minutos', () => {
    expect(backoffSeconds(30)).toBe(900)
  })

  it('bloqueia depois da 5ª falha e libera ao passar o tempo', () => {
    const t0 = 1_000_000
    for (let i = 0; i < 4; i++) recordLoginFailure(t0)
    expect(remainingLockSeconds(t0)).toBe(0)

    recordLoginFailure(t0)
    expect(remainingLockSeconds(t0)).toBe(30)
    expect(remainingLockSeconds(t0 + 31_000)).toBe(0)
  })

  it('login com sucesso zera o contador', () => {
    for (let i = 0; i < 5; i++) recordLoginFailure(1)
    clearLoginFailures()
    expect(remainingLockSeconds(2)).toBe(0)
    expect(recordLoginFailure(2)).toBe(0)
  })
})

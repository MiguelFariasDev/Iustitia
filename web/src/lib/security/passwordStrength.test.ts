import { describe, expect, it } from 'vitest'
import { passwordStrength } from './passwordStrength'

describe('passwordStrength', () => {
  it('senha vazia ou curta é muito fraca', () => {
    expect(passwordStrength('').score).toBe(0)
    expect(passwordStrength('abc').score).toBe(0)
  })

  it('repetição não conta como comprimento', () => {
    expect(passwordStrength('aaaaaaaaaaaaaaaaaaaa').score).toBeLessThanOrEqual(2)
  })

  it('frase-senha longa vence símbolo trocado em senha curta', () => {
    expect(passwordStrength('cavalo bateria grampo certo').score).toBeGreaterThan(
      passwordStrength('P@ssw0rd!').score
    )
    expect(passwordStrength('cavalo bateria grampo certo').score).toBe(4)
  })

  it('é monotônico com o comprimento para a mesma composição', () => {
    expect(passwordStrength('Abcdefg1hijklm').score).toBeGreaterThanOrEqual(
      passwordStrength('Abcdef1').score
    )
  })
})

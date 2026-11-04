import { getTribunalFromCnj, tribunalTemConsultaPublica } from './documento'

/**
 * Espelha TribunalResolverTests no backend, com os mesmos casos. A sigla decide em qual
 * índice do DataJud a consulta bate — errar aqui devolve "não encontrado" num processo que
 * existe, sem nada que denuncie a causa.
 */
describe('tribunal a partir do número CNJ', () => {
  it.each([
    ['0165738-43.2026.1.00.0000', 'STF'],
    ['0000832-35.2018.4.01.3202', 'TRF1'],
    ['1234567-48.2026.8.26.0001', 'TJSP'],
  ])('extrai o tribunal de %s', (numero, esperado) => {
    expect(getTribunalFromCnj(numero)).toBe(esperado)
  })

  it('funciona com ou sem máscara', () => {
    expect(getTribunalFromCnj('00008323520184013202')).toBe('TRF1')
    expect(getTribunalFromCnj('0000832-35.2018.4.01.3202')).toBe('TRF1')
  })

  it('devolve null para número incompleto', () => {
    expect(getTribunalFromCnj('123')).toBeNull()
    expect(getTribunalFromCnj('')).toBeNull()
  })

  it('devolve null para combinação que não existe', () => {
    // Segmento federal só tem TRF1..TRF6.
    expect(getTribunalFromCnj('00008320720184073202')).toBeNull()
  })
})

describe('tribunais atendidos pela consulta pública', () => {
  it('aceita os tribunais que estão no DataJud', () => {
    expect(tribunalTemConsultaPublica('TJSP')).toBe(true)
    expect(tribunalTemConsultaPublica('TRF1')).toBe(true)
    expect(tribunalTemConsultaPublica('TRT7')).toBe(true)
    expect(tribunalTemConsultaPublica('TRE-CE')).toBe(true)
  })

  /**
   * O STF não publica no DataJud. Sem esta checagem, o botão ficaria habilitado para um
   * número perfeitamente válido e a consulta voltaria erro — pior que o botão desabilitado
   * com a explicação no title.
   */
  it('recusa o STF, que não é atendido', () => {
    expect(tribunalTemConsultaPublica('STF')).toBe(false)
  })

  it('recusa null', () => {
    expect(tribunalTemConsultaPublica(null)).toBe(false)
  })
})

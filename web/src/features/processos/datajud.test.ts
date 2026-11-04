import { areaFromTribunal, toFormPatch, type ProcessoTribunal } from './datajud'

function processo(overrides: Partial<ProcessoTribunal> = {}): ProcessoTribunal {
  return {
    numeroProcesso: '0000832-35.2018.4.01.3202',
    tribunal: 'TRF1',
    grau: 'JE',
    dataAjuizamento: '2018-10-29',
    emSegredoDeJustica: false,
    classe: 'Procedimento do Juizado Especial Cível',
    sistema: 'Pje',
    orgaoJulgador: 'JEF Adj - Tefé',
    assuntos: ['Concessão'],
    movimentos: [],
    codigoMunicipioIbge: null,
    ...overrides,
  }
}

describe('auto-preenchimento a partir da consulta ao tribunal', () => {
  it('preenche tribunal, vara e data de distribuição', () => {
    const patch = toFormPatch(processo())

    expect(patch.tribunal).toBe('TRF1')
    expect(patch.vara).toBe('JEF Adj - Tefé')
    expect(patch.dataDistribuicao).toBe('2018-10-29')
  })

  it('junta classe e assunto principal no campo de assunto', () => {
    expect(toFormPatch(processo()).assunto).toBe(
      'Procedimento do Juizado Especial Cível — Concessão'
    )
  })

  it('usa só a classe quando não há assunto', () => {
    expect(toFormPatch(processo({ assuntos: [] })).assunto).toBe(
      'Procedimento do Juizado Especial Cível'
    )
  })

  /**
   * Campo ausente na resposta NÃO entra no patch: se entrasse como string vazia, apagaria o
   * que a advogada já tinha digitado — o auto-preenchimento viraria auto-apagamento.
   */
  it('omite os campos que o tribunal não informou', () => {
    const patch = toFormPatch(
      processo({ orgaoJulgador: null, dataAjuizamento: null, classe: null, assuntos: [] })
    )

    expect(patch).not.toHaveProperty('vara')
    expect(patch).not.toHaveProperty('dataDistribuicao')
    expect(patch).not.toHaveProperty('assunto')
    expect(patch.tribunal).toBe('TRF1')
  })

  /** Cliente e responsável são decisão do escritório, não do tribunal. */
  it('nunca preenche cliente nem responsável', () => {
    const patch = toFormPatch(processo())

    expect(patch).not.toHaveProperty('clienteId')
    expect(patch).not.toHaveProperty('responsavelUserId')
  })
})

describe('mapeamento ampliado dos campos do DataJud', () => {
  it('infere a área trabalhista para o TRT7 (Ceará) e TRT22 (Piauí)', () => {
    expect(toFormPatch(processo({ tribunal: 'TRT7' })).area).toBe('Trabalhista')
    expect(toFormPatch(processo({ tribunal: 'TRT22' })).area).toBe('Trabalhista')
  })

  it('não chuta a área para TJCE, TJPI ou TRF5', () => {
    for (const tribunal of ['TJCE', 'TJPI', 'TRF5']) {
      expect(areaFromTribunal(tribunal)).toBeNull()
      expect(toFormPatch(processo({ tribunal })).area).toBeUndefined()
    }
  })

  it('registra grau, sistema e último movimento nas observações', () => {
    const patch = toFormPatch(
      processo({
        tribunal: 'TJCE',
        grau: 'G1',
        sistema: 'PJe',
        assuntos: ['Concessão', 'Auxílio-doença'],
        movimentos: [{ nome: 'Sentença', dataHora: '2024-03-05T10:00:00Z' }],
      })
    )

    expect(patch.observacoes).toContain('1º grau · PJe')
    expect(patch.observacoes).toContain('Assuntos: Concessão; Auxílio-doença')
    expect(patch.observacoes).toContain('Último movimento: Sentença (05/03/2024)')
  })

  it('não gera observações quando o tribunal não informou nada', () => {
    const patch = toFormPatch(processo({ grau: null, sistema: null, assuntos: [], movimentos: [] }))
    expect(patch.observacoes).toBeUndefined()
  })
})

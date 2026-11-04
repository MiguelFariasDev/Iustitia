import { csvCell, toCsv } from './csv'

describe('serialização CSV', () => {
  it('escapa aspas, separador e quebras de linha', () => {
    expect(csvCell('diz "olá"')).toBe('"diz ""olá"""')
    expect(csvCell('a;b')).toBe('"a;b"')
    expect(csvCell('linha1\nlinha2')).toBe('"linha1\nlinha2"')
  })

  it.each(['=SOMA(A1:A9)', '+55 11 9999', '-2+3', '@cmd', '\tcmd'])(
    'neutraliza fórmula de planilha: %j',
    (value) => {
      // Um nome de cliente começando com "=" seria executado pelo Excel/Sheets ao abrir o arquivo.
      expect(csvCell(value).replace(/^"/, '')).toMatch(/^'/)
    }
  )

  it('trata nulos e booleanos', () => {
    expect(csvCell(null)).toBe('')
    expect(csvCell(undefined)).toBe('')
    expect(csvCell(true)).toBe('true')
    expect(csvCell(0)).toBe('0')
  })

  it('monta cabeçalho e linhas com BOM e ponto e vírgula (Excel pt-BR)', () => {
    const csv = toCsv(
      ['A', 'B'],
      [
        ['1', 'dois'],
        ['3', null],
      ]
    )

    expect(csv.startsWith('﻿')).toBe(true)
    expect(csv.slice(1).split('\r\n')).toEqual(['A;B', '1;dois', '3;'])
  })
})

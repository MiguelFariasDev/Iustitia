import { analisarPlanilha, parseCsv } from './importacao'

const hoje = new Date('2026-09-30T12:00:00Z')

describe('parseCsv', () => {
  it('lê ponto e vírgula, aspas e BOM', () => {
    const csv = '﻿a;b\n"x;1";"diz ""oi"""\n'
    expect(parseCsv(csv)).toEqual([
      ['a', 'b'],
      ['x;1', 'diz "oi"'],
    ])
  })

  it('detecta vírgula como delimitador e ignora linhas vazias', () => {
    expect(parseCsv('a,b\n\n1,2\r\n')).toEqual([
      ['a', 'b'],
      ['1', '2'],
    ])
  })
})

describe('analisarPlanilha', () => {
  const cab = 'numero_cnj;documento_cliente;area;data_distribuicao;valor_causa'

  it('rejeita planilha sem colunas obrigatórias', () => {
    expect(analisarPlanilha(parseCsv('a;b\n1;2'), hoje).erroCabecalho).toMatch(/numero_cnj/)
  })

  it('aceita linha válida do CE, aceita área com acento e data brasileira', () => {
    // 0000832-35.2018.4.01.3202 é o exemplo do DataJud (dígito verificador válido).
    const { linhas } = analisarPlanilha(
      parseCsv(`${cab}\n0000832-35.2018.4.01.3202;123.456.789-09;Cível;29/10/2018;1.000,50`),
      hoje
    )
    expect(linhas[0]!.erros).toEqual([])
    expect(linhas[0]!.valores.area).toBe('Civel')
    expect(linhas[0]!.valores.dataDistribuicao).toBe('2018-10-29')
    expect(linhas[0]!.valores.tribunal).toBe('TRF1')
  })

  it('aponta CNJ inválido, documento curto, área desconhecida e data futura', () => {
    const { linhas } = analisarPlanilha(
      parseCsv(`${cab}\n123;111;Inexistente;2030-01-01;abc`),
      hoje
    )
    expect(linhas[0]!.erros).toHaveLength(5)
  })

  it('marca a repetição dentro do arquivo', () => {
    const l = '0000832-35.2018.4.01.3202;12345678909;Civel;;'
    const { linhas } = analisarPlanilha(parseCsv(`${cab}\n${l}\n${l}`), hoje)
    expect(linhas.map((x) => x.duplicadaNoArquivo)).toEqual([false, true])
  })
})

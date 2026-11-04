import { descreverSerie } from './descricao'
import { mockIndicadores, rotulosEvolucao } from './mocks'
import type { Periodo } from './types'

const soma = (itens: { total: number }[]) => itens.reduce((s, i) => s + i.total, 0)

describe('mockIndicadores', () => {
  it.each(['mes', 'trimestre', 'ano'] as Periodo[])(
    '%s: os números não se contradizem',
    (periodo) => {
      const d = mockIndicadores(periodo)

      expect(soma(d.processosPorArea)).toBe(soma(d.processosPorStatus))
      expect(d.kpis.processosAtivos).toBe(
        soma(d.produtividade.map((p) => ({ total: p.processosAtivos })))
      )
      expect(d.kpis.tarefasConcluidas).toBe(
        soma(d.produtividade.map((p) => ({ total: p.tarefasConcluidas })))
      )
    }
  )

  it('períodos maiores acumulam mais tarefas concluídas; a carteira não muda', () => {
    const mes = mockIndicadores('mes')
    const ano = mockIndicadores('ano')

    expect(ano.kpis.tarefasConcluidas).toBe(mes.kpis.tarefasConcluidas * 12)
    expect(ano.processosPorArea).toEqual(mes.processosPorArea)
  })

  it('a série de publicações tem um ponto por rótulo do período', () => {
    expect(mockIndicadores('mes').evolucaoPublicacoes).toHaveLength(4)
    expect(mockIndicadores('trimestre').evolucaoPublicacoes).toHaveLength(3)
    expect(mockIndicadores('ano').evolucaoPublicacoes).toHaveLength(12)
  })
})

describe('rotulosEvolucao', () => {
  it('mês atual usa semanas; os demais, os meses até o corrente', () => {
    const agora = new Date(2026, 10, 4)

    expect(rotulosEvolucao('mes', agora)).toEqual(['Sem 1', 'Sem 2', 'Sem 3', 'Sem 4'])
    expect(rotulosEvolucao('trimestre', agora)).toEqual(['set', 'out', 'nov'])
    expect(rotulosEvolucao('ano', agora)[11]).toBe('nov')
    expect(rotulosEvolucao('ano', agora)[0]).toBe('dez')
  })
})

describe('descreverSerie', () => {
  it('lê título e valores', () => {
    expect(
      descreverSerie('Processos por área', [
        { rotulo: 'Cível', total: 31 },
        { rotulo: 'Trabalhista', total: 24 },
      ])
    ).toBe('Processos por área: Cível 31, Trabalhista 24.')
  })

  it('diz que não há dados em vez de calar', () => {
    expect(descreverSerie('Evolução', [])).toBe('Evolução: sem dados no período.')
  })
})

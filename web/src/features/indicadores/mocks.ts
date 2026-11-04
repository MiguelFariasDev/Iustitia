import { ultimosMeses } from '@/mocks/dates'
import type { Indicadores, ItemSerie, Periodo, Produtividade } from './types'

/**
 * DADOS DE DEMONSTRAÇÃO. Não existe backend de relatórios, e prazos e tarefas só entram nas
 * Fases 3 e 4. Os números são FIXOS e coerentes entre si (a soma das áreas é a soma dos
 * status; os ativos por advogado somam o KPI), para a tela não se contradizer.
 *
 * TODO(indicadores-backend): trocar por endpoint agregado de relatórios (módulo Reporting) e
 * remover o aviso de demonstração.
 */
const processosPorArea: ItemSerie[] = [
  { rotulo: 'Cível', total: 31 },
  { rotulo: 'Trabalhista', total: 24 },
  { rotulo: 'Família', total: 12 },
  { rotulo: 'Previdenciário', total: 9 },
  { rotulo: 'Criminal', total: 8 },
  { rotulo: 'Outros', total: 6 },
]

const processosPorStatus: ItemSerie[] = [
  { rotulo: 'Ativo', total: 48 },
  { rotulo: 'Suspenso', total: 6 },
  { rotulo: 'Encerrado', total: 14 },
  { rotulo: 'Arquivado', total: 22 },
]

/** Quantos meses/semanas cada período mostra e o volume de tarefas que ele acumula. */
const configuracaoPeriodo: Record<Periodo, { pontos: number; fator: number }> = {
  mes: { pontos: 4, fator: 1 },
  trimestre: { pontos: 3, fator: 3 },
  ano: { pontos: 12, fator: 12 },
}

/** Rótulos relativos a `agora`: o gráfico nunca fica com meses velhos. */
export function rotulosEvolucao(periodo: Periodo, agora: Date = new Date()): string[] {
  const { pontos } = configuracaoPeriodo[periodo]
  if (periodo === 'mes') return Array.from({ length: pontos }, (_, i) => `Sem ${i + 1}`)

  return ultimosMeses(pontos, agora)
}

/** Série determinística (sem Math.random): a mesma tela renderiza o mesmo gráfico. */
const base = [18, 24, 21, 30, 27, 35, 31, 38, 34, 41, 37, 44]

function evolucao(periodo: Periodo): ItemSerie[] {
  const rotulos = rotulosEvolucao(periodo)
  const inicio = base.length - rotulos.length

  return rotulos.map((rotulo, i) => ({ rotulo, total: base[inicio + i]! }))
}

const equipe: Omit<Produtividade, 'tarefasConcluidas'>[] = [
  { id: 'adv-1', nome: 'Ana Souza', processosAtivos: 22, tarefasPendentes: 9 },
  { id: 'adv-2', nome: 'Bruno Carvalho', processosAtivos: 15, tarefasPendentes: 6 },
  { id: 'adv-3', nome: 'Carla Mendes', processosAtivos: 11, tarefasPendentes: 4 },
]

/** Tarefas concluídas por mês, por advogado — escaladas pelo período. */
const concluidasPorMes: Record<string, number> = { 'adv-1': 14, 'adv-2': 10, 'adv-3': 7 }

export function mockIndicadores(periodo: Periodo): Indicadores {
  const { fator } = configuracaoPeriodo[periodo]
  const produtividade = equipe.map((p) => ({
    ...p,
    tarefasConcluidas: concluidasPorMes[p.id]! * fator,
  }))

  return {
    kpis: {
      processosAtivos: processosPorStatus.find((s) => s.rotulo === 'Ativo')!.total,
      prazosVencendo: 7,
      tarefasConcluidas: produtividade.reduce((soma, p) => soma + p.tarefasConcluidas, 0),
    },
    processosPorArea: structuredClone(processosPorArea),
    processosPorStatus: structuredClone(processosPorStatus),
    evolucaoPublicacoes: evolucao(periodo),
    produtividade,
  }
}

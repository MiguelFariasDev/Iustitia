export type Periodo = 'mes' | 'trimestre' | 'ano'

export const periodos: readonly Periodo[] = ['mes', 'trimestre', 'ano']

export const periodoLabels: Record<Periodo, string> = {
  mes: 'Mês atual',
  trimestre: 'Últimos 3 meses',
  ano: 'Ano',
}

export interface ItemSerie {
  rotulo: string
  total: number
}

export interface Produtividade {
  id: string
  nome: string
  processosAtivos: number
  tarefasPendentes: number
  tarefasConcluidas: number
}

export interface Indicadores {
  kpis: {
    processosAtivos: number
    /** Prazos que vencem nos próximos 7 dias. */
    prazosVencendo: number
    /** Tarefas concluídas no período escolhido. */
    tarefasConcluidas: number
  }
  /** Carteira atual: não depende do período. */
  processosPorArea: ItemSerie[]
  /** Carteira atual: não depende do período. */
  processosPorStatus: ItemSerie[]
  evolucaoPublicacoes: ItemSerie[]
  produtividade: Produtividade[]
}

/** Modelo de exibição do processo — o contrato da API é traduzido em contract.ts. */

export type AreaDireito =
  | 'Civel'
  | 'Trabalhista'
  | 'Criminal'
  | 'Tributario'
  | 'Previdenciario'
  | 'Familia'
  | 'Consumidor'
  | 'Empresarial'
  | 'Administrativo'
  | 'Ambiental'
  | 'Outros'

export type StatusProcesso = 'Ativo' | 'Suspenso' | 'Encerrado' | 'Arquivado'

export interface Processo {
  id: string
  /** Já mascarado pela API: NNNNNNN-DD.AAAA.J.TR.OOOO. */
  cnjNumber: string
  clienteId: string
  responsavelUserId: string
  area: AreaDireito
  status: StatusProcesso
  tribunal: string | null
  comarca: string | null
  vara: string | null
  assunto: string | null
  parteContraria: string | null
  valorCausa: number | null
  dataDistribuicao: string | null
  observacoes: string | null
  /** Se o processo está sendo consultado no diário. Ativo e suspenso geram captura. */
  geraCaptura: boolean
  createdAt: string
}

export interface ProcessoListItem {
  id: string
  cnjNumber: string
  clienteId: string
  area: AreaDireito
  status: StatusProcesso
  tribunal: string | null
  parteContraria: string | null
  dataDistribuicao: string | null
}

export interface ProcessosPage {
  items: ProcessoListItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ProcessosFilters {
  page?: number
  pageSize?: number
  clienteId?: string | null
  area?: AreaDireito | null
  status?: StatusProcesso | null
  busca?: string
  responsavelUserId?: string | null
  tribunal?: string
  /** Datas ISO (AAAA-MM-DD). */
  distribuicaoDe?: string
  distribuicaoAte?: string
}

export const areaDireitoLabels: Record<AreaDireito, string> = {
  Civel: 'Cível',
  Trabalhista: 'Trabalhista',
  Criminal: 'Criminal',
  Tributario: 'Tributário',
  Previdenciario: 'Previdenciário',
  Familia: 'Família',
  Consumidor: 'Consumidor',
  Empresarial: 'Empresarial',
  Administrativo: 'Administrativo',
  Ambiental: 'Ambiental',
  Outros: 'Outros',
}

export const areasDireito = Object.keys(areaDireitoLabels) as AreaDireito[]

/**
 * A cor acompanha o que o status significa para a CAPTURA: verde para o que está sendo
 * acompanhado, neutro para o que já não gera publicação. O texto sempre existe — a cor
 * nunca é o único indicador.
 */
export const statusProcessoMeta: Record<
  StatusProcesso,
  { label: string; tone: 'success' | 'warning' | 'neutral'; descricao: string }
> = {
  Ativo: { label: 'Ativo', tone: 'success', descricao: 'Publicações são capturadas.' },
  Suspenso: {
    label: 'Suspenso',
    tone: 'warning',
    descricao: 'Sobrestado, mas as publicações continuam sendo capturadas.',
  },
  Encerrado: {
    label: 'Encerrado',
    tone: 'neutral',
    descricao: 'Não gera mais captura de publicações.',
  },
  Arquivado: {
    label: 'Arquivado',
    tone: 'neutral',
    descricao: 'Estado final. Não gera mais captura de publicações.',
  },
}

export const statusProcesso = Object.keys(statusProcessoMeta) as StatusProcesso[]

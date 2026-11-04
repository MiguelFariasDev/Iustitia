import {
  areaDireitoLabels,
  statusProcessoMeta,
  type AreaDireito,
  type Processo,
  type ProcessoListItem,
  type StatusProcesso,
} from './types'

/** Camada anticorrupção entre Legal.Api e o modelo da tela. */

/** ProcessoResponse (Legal.Application.Features.Processos). */
export interface ProcessoDto {
  id: string
  cnjNumber: string
  clienteId: string
  responsavelUserId: string
  area: string
  status: string
  tribunal: string | null
  comarca: string | null
  vara: string | null
  assunto: string | null
  parteContraria: string | null
  valorCausa: number | null
  dataDistribuicao: string | null
  observacoes: string | null
  geraCaptura: boolean
  createdAt: string
}

/** ProcessoListItem (Legal.Application.Features.Processos). */
export interface ProcessoListItemDto {
  id: string
  cnjNumber: string
  clienteId: string
  area: string
  status: string
  tribunal: string | null
  parteContraria: string | null
  dataDistribuicao: string | null
}

export interface ListProcessosDto {
  items: ProcessoListItemDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

function toArea(apiArea: string): AreaDireito {
  return apiArea in areaDireitoLabels ? (apiArea as AreaDireito) : 'Outros'
}

/**
 * Status desconhecido cai em 'Ativo', não em 'Arquivado'.
 *
 * Diante de um valor que não sabemos interpretar, o comportamento seguro é tratar o processo
 * como EM ACOMPANHAMENTO: exibi-lo como arquivado esconderia da advogada um caso que talvez
 * tenha prazo correndo — o erro caro.
 */
function toStatus(apiStatus: string): StatusProcesso {
  return apiStatus in statusProcessoMeta ? (apiStatus as StatusProcesso) : 'Ativo'
}

export function toProcesso(dto: ProcessoDto): Processo {
  return {
    ...dto,
    area: toArea(dto.area),
    status: toStatus(dto.status),
  }
}

export function toProcessoListItem(dto: ProcessoListItemDto): ProcessoListItem {
  return {
    ...dto,
    area: toArea(dto.area),
    status: toStatus(dto.status),
  }
}

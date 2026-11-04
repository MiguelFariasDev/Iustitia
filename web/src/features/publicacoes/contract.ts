import type { PagedList } from '@/lib/api/types'
import type {
  Publication,
  PublicationKind,
  PublicationMetadata,
  PublicationStatus,
  PublicationsSummary,
} from './types'

/**
 * Camada anticorrupção entre o contrato da API .NET e o modelo de exibição da tela.
 *
 * Os dois vocabulários não coincidem, e não deveriam: a API fala o domínio jurídico
 * (`PendingReview`, `Intimacao`), a tela fala o fluxo de trabalho do escritório
 * (`untreated`, `intimation`). Traduzir aqui mantém a UI estável quando o domínio evoluir —
 * e evita espalhar `status === 'PendingReview'` por componentes.
 */

/** Item da listagem devolvido por GET /publications (ListPublicationsResponse). */
export interface PublicationListItemDto {
  id: string
  processId: string | null
  clienteId: string | null
  clienteNome: string | null
  responsavelUserId: string | null
  tribunal: string | null
  cnjNumber: string
  source: string
  /** Trecho do texto. A listagem NUNCA devolve o inteiro teor (dado sob sigilo). */
  contentPreview: string
  publishedAt: string
  capturedAt: string
  type: string | null
  status: string
  isRead: boolean
}

/** Detalhe devolvido por GET /publications/{id} (GetPublicationByIdResponse). */
export interface PublicationDetailDto extends Omit<PublicationListItemDto, 'contentPreview'> {
  tenantId: string
  externalId: string
  /** Inteiro teor — só a consulta por id devolve. */
  rawContent: string
  metadataJson: string | null
  reviewNotes: string | null
}

export type PublicationsListDto = Pick<
  PagedList<PublicationListItemDto>,
  'items' | 'page' | 'pageSize' | 'totalCount' | 'totalPages'
>

/** GET /publications/summary (GetPublicationsSummaryResponse). */
export interface PublicationsSummaryDto {
  total: number
  pendingReview: number
  unread: number
  pendingToday: number
  treatedThisWeek: number
}

/** Valores de `status` aceitos por PATCH /publications/{id}/status. */
export type PublicationApiStatus = 'Approved' | 'Corrected' | 'Rejected' | 'Archived'

const statusFromApi: Record<string, PublicationStatus> = {
  PendingReview: 'untreated',
  Approved: 'treated',
  Corrected: 'treated',
  Rejected: 'discarded',
  Archived: 'archived',
}

/** Filtro da tela -> valor(es) do parâmetro `status` da API. */
export const statusToApiFilter: Record<PublicationStatus, string> = {
  untreated: 'PendingReview',
  treated: 'Approved,Corrected',
  discarded: 'Rejected',
  archived: 'Archived',
}

const kindFromApi: Record<string, PublicationKind> = {
  Intimacao: 'intimation',
  Decisao: 'decision',
  Sentenca: 'sentence',
  Despacho: 'other',
  Acordao: 'decision',
  Outros: 'other',
}

/**
 * `status` desconhecido cai em 'untreated', não em 'treated': diante de um valor que não
 * sabemos interpretar, o comportamento seguro num sistema de prazos é mostrar a publicação
 * como PENDENTE. Esconder algo que talvez precise de ação é o erro caro.
 */
export function toPublication(
  dto: PublicationListItemDto,
  content = dto.contentPreview
): Publication {
  return {
    id: dto.id,
    publishedAt: dto.publishedAt,
    capturedAt: dto.capturedAt,
    kind: (dto.type && kindFromApi[dto.type]) || 'other',
    processId: dto.processId,
    processNumber: dto.cnjNumber || null,
    tribunal: dto.tribunal,
    journal: dto.source,
    clienteId: dto.clienteId,
    clienteNome: dto.clienteNome,
    status: statusFromApi[dto.status] ?? 'untreated',
    isRead: dto.isRead,
    content,
    reviewNotes: null,
    metadata: null,
  }
}

export function toPublicationDetail(dto: PublicationDetailDto): Publication {
  return {
    ...toPublication({ ...dto, contentPreview: dto.rawContent }, dto.rawContent),
    reviewNotes: dto.reviewNotes,
    metadata: parseMetadata(dto.metadataJson),
  }
}

export function toPublicationsSummary(dto: PublicationsSummaryDto): PublicationsSummary {
  return {
    total: dto.total,
    untreatedTotal: dto.pendingReview,
    unread: dto.unread,
    untreatedToday: dto.pendingToday,
    treatedThisWeek: dto.treatedThisWeek,
  }
}

const asText = (value: unknown) => (typeof value === 'string' && value ? value : null)

/**
 * O link só é aceito se for https: o metadado vem de uma fonte externa, e um `javascript:` ou
 * `data:` num href seria execução de código por clique. Qualquer outro esquema vira "sem link".
 */
function safeHttpsLink(value: unknown): string | null {
  const text = asText(value)
  if (!text) return null
  try {
    return new URL(text).protocol === 'https:' ? text : null
  } catch {
    return null
  }
}

export function parseMetadata(json: string | null): PublicationMetadata | null {
  if (!json) return null

  try {
    const raw: unknown = JSON.parse(json)
    if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return null
    const data = raw as Record<string, unknown>

    return {
      orgao: asText(data.orgao),
      classe: asText(data.classe),
      meio: asText(data.meio),
      tipoDocumento: asText(data.tipoDocumento),
      numeroComunicacao: asText(data.numeroComunicacao),
      link: safeHttpsLink(data.link),
      raw: data,
    }
  } catch {
    return null
  }
}

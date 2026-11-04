/**
 * Como a TELA chama o estado de revisão da publicação — vocabulário do fluxo de trabalho do
 * escritório, não do domínio (ver contract.ts, que faz a tradução).
 */
export type PublicationStatus = 'untreated' | 'treated' | 'discarded' | 'archived'

export const publicationStatuses: PublicationStatus[] = [
  'untreated',
  'treated',
  'discarded',
  'archived',
]

export type PublicationKind =
  'intimation' | 'citation' | 'decision' | 'sentence' | 'hearing' | 'other'

export interface Publication {
  id: string
  /** Data de divulgação no diário oficial (ISO). */
  publishedAt: string
  capturedAt: string
  kind: PublicationKind
  /** `null` quando a publicação ainda não foi vinculada a um processo cadastrado. */
  processId: string | null
  processNumber: string | null
  /** Sigla do tribunal (TJSP, TRF3…), derivada do número CNJ. */
  tribunal: string | null
  /** Diário de origem (DJEN). */
  journal: string
  clienteId: string | null
  clienteNome: string | null
  status: PublicationStatus
  isRead: boolean
  /** Trecho na listagem; íntegra no detalhe. */
  content: string
  /** Só no detalhe. */
  reviewNotes: string | null
  /** Só no detalhe: órgão, classe, link do DJEN etc., já interpretados. */
  metadata: PublicationMetadata | null
}

export interface PublicationMetadata {
  orgao: string | null
  classe: string | null
  meio: string | null
  tipoDocumento: string | null
  numeroComunicacao: string | null
  /** Link da comunicação no DJEN (https). */
  link: string | null
  /** JSON bruto, para a aba "Metadados". */
  raw: Record<string, unknown>
}

export interface PublicationsSummary {
  total: number
  /** Não tratadas. */
  untreatedTotal: number
  unread: number
  untreatedToday: number
  treatedThisWeek: number
}

export type PublicationSort = 'recent' | 'oldest' | 'tribunal' | 'tribunal-desc'

export interface PublicationsFilters {
  page?: number
  pageSize?: number
  status?: PublicationStatus | null
  tribunal?: string | null
  clienteId?: string | null
  processId?: string | null
  /** Datas de divulgação, ISO yyyy-MM-dd, inclusivas. */
  from?: string | null
  to?: string | null
  isRead?: boolean | null
  busca?: string | null
  ordem?: PublicationSort | null
}

export interface PublicationsPage {
  items: Publication[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

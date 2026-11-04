import { daysFromNow } from '@/mocks/dates'
import type { PublicationApiStatus } from './contract'
import type {
  Publication,
  PublicationsFilters,
  PublicationsPage,
  PublicationsSummary,
  PublicationStatus,
} from './types'

/**
 * Backend simulado, EM MEMÓRIA, para desenvolver e rodar os E2E sem a API .NET
 * (NEXT_PUBLIC_API_MOCKS=true). Reimplementa as regras que a API aplica de verdade — filtros,
 * ordenação, paginação e transições de status — para a UI ser exercitada como em produção.
 *
 * É estado de módulo: uma recarga da página volta aos dados iniciais, como um banco de teste.
 */
const textos = [
  'Intime-se a parte ré, na pessoa de seu advogado, para apresentar contestação no prazo de 15 (quinze) dias úteis, sob pena de revelia e confissão quanto à matéria de fato.',
  'Vistos. Designo audiência de conciliação para o dia indicado, devendo as partes comparecer acompanhadas de seus advogados. Intimem-se.',
  'Sentença: JULGO PROCEDENTE o pedido para condenar a ré ao pagamento da quantia pleiteada, acrescida de correção monetária e juros de mora.',
  'Despacho: Manifeste-se a parte autora sobre a certidão do oficial de justiça no prazo de 5 (cinco) dias.',
  'Decisão interlocutória: defiro o pedido de tutela de urgência para determinar a suspensão dos descontos até o julgamento final.',
]

const clientes = [
  { id: 'cli-1', nome: 'Maria Aparecida Silva' },
  { id: 'cli-2', nome: 'Transportadora Horizonte Ltda.' },
  { id: 'cli-3', nome: 'João Pedro Albuquerque' },
]

const tribunais = ['TJSP', 'TJRJ', 'TJRS', 'TRF3', 'TRT2']
const tipos: Publication['kind'][] = ['intimation', 'decision', 'sentence', 'other', 'intimation']

function seed(): Publication[] {
  return Array.from({ length: 27 }, (_, i) => {
    const cliente = clientes[i % clientes.length]!
    const publishedAt = daysFromNow(-(i % 12), 8, 0)
    const numero = `500${String(1000 + i).padStart(4, '0')}-${String(10 + (i % 80)).padStart(2, '0')}.2026.8.26.0001`

    return {
      id: `pub-${String(i + 1).padStart(3, '0')}`,
      publishedAt,
      capturedAt: publishedAt,
      kind: tipos[i % tipos.length]!,
      processId: `proc-${(i % 9) + 1}`,
      processNumber: numero,
      tribunal: tribunais[i % tribunais.length]!,
      journal: 'DJEN',
      clienteId: cliente.id,
      clienteNome: cliente.nome,
      status: 'untreated',
      isRead: i % 3 === 0,
      content: textos[i % textos.length]!,
      reviewNotes: null,
      metadata: {
        orgao: `${(i % 5) + 1}ª Vara Cível`,
        classe: 'Procedimento Comum Cível',
        meio: 'D',
        tipoDocumento: 'Intimação',
        numeroComunicacao: String(100 + i),
        link: 'https://comunicaapi.pje.jus.br/',
        raw: { orgao: `${(i % 5) + 1}ª Vara Cível`, classe: 'Procedimento Comum Cível' },
      },
    }
  })
}

let store: Publication[] = seed()

/** Só para testes: volta ao estado inicial. */
export function resetPublicationsMock() {
  store = seed()
}

function matches(item: Publication, filters: PublicationsFilters): boolean {
  if (filters.status && item.status !== filters.status) return false
  if (filters.tribunal && item.tribunal !== filters.tribunal) return false
  if (filters.clienteId && item.clienteId !== filters.clienteId) return false
  if (filters.processId && item.processId !== filters.processId) return false
  if (filters.isRead !== null && filters.isRead !== undefined && item.isRead !== filters.isRead) {
    return false
  }
  const day = item.publishedAt.slice(0, 10)
  if (filters.from && day < filters.from) return false
  if (filters.to && day > filters.to) return false
  if (filters.busca) {
    const needle = filters.busca.toLowerCase()
    const digits = needle.replace(/\D/g, '')
    const byNumber =
      digits.length >= 3 && (item.processNumber ?? '').replace(/\D/g, '').includes(digits)
    const byCliente = (item.clienteNome ?? '').toLowerCase().includes(needle)
    if (!byNumber && !byCliente) return false
  }
  return true
}

function sorted(items: Publication[], ordem: PublicationsFilters['ordem']) {
  const byDate = (a: Publication, b: Publication) => b.publishedAt.localeCompare(a.publishedAt)
  const copy = [...items]
  switch (ordem) {
    case 'oldest':
      return copy.sort((a, b) => -byDate(a, b))
    case 'tribunal':
      return copy.sort((a, b) => (a.tribunal ?? '').localeCompare(b.tribunal ?? '') || byDate(a, b))
    case 'tribunal-desc':
      return copy.sort((a, b) => (b.tribunal ?? '').localeCompare(a.tribunal ?? '') || byDate(a, b))
    default:
      return copy.sort(byDate)
  }
}

export function mockListPublications(filters: PublicationsFilters): PublicationsPage {
  const page = filters.page && filters.page > 0 ? filters.page : 1
  const pageSize = filters.pageSize && filters.pageSize > 0 ? filters.pageSize : 20
  const all = sorted(
    store.filter((item) => matches(item, filters)),
    filters.ordem
  )

  return {
    items: all.slice((page - 1) * pageSize, page * pageSize).map((item) => ({ ...item })),
    page,
    pageSize,
    totalCount: all.length,
    totalPages: Math.ceil(all.length / pageSize),
  }
}

export function mockPublicationsSummary(): PublicationsSummary {
  const today = new Date().toISOString().slice(0, 10)
  const untreated = store.filter((item) => item.status === 'untreated')

  return {
    total: store.length,
    untreatedTotal: untreated.length,
    unread: store.filter((item) => !item.isRead).length,
    untreatedToday: untreated.filter((item) => item.publishedAt.startsWith(today)).length,
    treatedThisWeek: store.filter((item) => item.status === 'treated' || item.status === 'archived')
      .length,
  }
}

export function mockGetPublication(id: string): Publication | null {
  const found = store.find((item) => item.id === id)
  return found ? { ...found } : null
}

export function mockMarkAsRead(id: string): 'ok' | 'not-found' | 'already-read' {
  const found = store.find((item) => item.id === id)
  if (!found) return 'not-found'
  if (found.isRead) return 'already-read'
  found.isRead = true
  return 'ok'
}

const apiToStatus: Record<PublicationApiStatus, PublicationStatus> = {
  Approved: 'treated',
  Corrected: 'treated',
  Rejected: 'discarded',
  Archived: 'archived',
}

/** Mesmas transições de Publication.ChangeStatus: de pendente para qualquer desfecho; de desfecho só para arquivada. */
export function mockChangeStatus(
  id: string,
  status: PublicationApiStatus,
  notes?: string
): 'ok' | 'not-found' | 'invalid-transition' {
  const found = store.find((item) => item.id === id)
  if (!found) return 'not-found'

  const target = apiToStatus[status]
  const allowed =
    found.status === 'untreated' ? true : found.status !== 'archived' && target === 'archived'
  if (!allowed || found.status === target) return 'invalid-transition'

  found.status = target
  found.reviewNotes = notes ?? null
  return 'ok'
}

import { getPublications } from './api'
import { kindMeta, statusMeta } from './presentation'
import { toCsv } from '@/lib/csv'
import { formatDate } from '@/lib/format'
import type { Publication, PublicationsFilters } from './types'

/** Teto de segurança: 50 páginas de 100. Acima disso a exportação seria lenta e provavelmente enganada por filtro amplo. */
const MAX_PAGES = 50
const EXPORT_PAGE_SIZE = 100

/**
 * Exporta TODAS as publicações do filtro atual (não só a página visível), percorrendo as
 * páginas da API. O inteiro teor NÃO sai: a exportação é uma planilha de controle (data,
 * processo, tribunal, cliente, status) e levar o texto sigiloso para um arquivo solto,
 * fora do sistema, não tem contrapartida de auditoria.
 */
export async function exportPublicationsCsv(filters: PublicationsFilters): Promise<string> {
  const rows: Publication[] = []

  for (let page = 1; page <= MAX_PAGES; page++) {
    const result = await getPublications({ ...filters, page, pageSize: EXPORT_PAGE_SIZE })
    rows.push(...result.items)
    if (page >= result.totalPages) break
  }

  return publicationsToCsv(rows)
}

export function publicationsToCsv(items: Publication[]): string {
  return toCsv(
    ['Divulgado em', 'Tipo', 'Processo', 'Tribunal', 'Cliente', 'Status', 'Lida'],
    items.map((item) => [
      formatDate(item.publishedAt),
      kindMeta[item.kind].label,
      item.processNumber,
      item.tribunal,
      item.clienteNome,
      statusMeta[item.status].label,
      item.isRead ? 'Sim' : 'Não',
    ])
  )
}

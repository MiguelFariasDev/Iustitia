import type { Publication } from '@/features/publicacoes/types'
import { sugerirDemo, sugestaoDaApi, type Sugestao } from './sugestao'

export type Decisao = 'accepted' | 'corrected' | 'rejected'

/** O que ficou decidido para uma publicação (guardado só na sessão do navegador). */
export interface Resultado {
  decisao: Decisao
  /** Prazo salvo, em dias úteis; `null` quando a sugestão foi rejeitada. */
  dias: number | null
  /** ISO yyyy-MM-dd do vencimento salvo; `null` quando rejeitada. */
  vencimento: string | null
  /** Quantos dias a IA sugeriu originalmente (para auditoria da correção). */
  sugeridoDias: number
}

export interface ItemRevisao {
  publication: Publication
  sugestao: Sugestao
}

/**
 * Fila de revisão, as publicações mais recentes primeiro.
 *
 * - Com a API real, entra quem tem a sugestão da IA (`publication.suggestion`); as pendentes que a IA ainda
 *   não analisou não entram (ver `contarAguardandoIa`) — não se inventa prazo para um escritório real.
 * - Em demonstração (mocks), só publicações com processo, com a sugestão montada por `sugerirDemo`.
 */
export function montarFila(publications: readonly Publication[], demo = false): ItemRevisao[] {
  const ordenadas = [...publications].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt))

  if (demo) {
    return ordenadas
      .filter((publication) => publication.processId !== null)
      .map((publication) => ({ publication, sugestao: sugerirDemo(publication) }))
  }

  return ordenadas.flatMap((publication) =>
    publication.suggestion ? [{ publication, sugestao: sugestaoDaApi(publication.suggestion) }] : []
  )
}

/** Pendentes que a IA ainda não analisou (só faz sentido com a API real). */
export function contarAguardandoIa(publications: readonly Publication[]): number {
  return publications.filter((publication) => !publication.suggestion).length
}

/**
 * Para onde ir depois de decidir `atualId`: a próxima ainda não decidida DEPOIS dela; se não houver,
 * a primeira ainda não decidida; se tudo foi decidido, fica onde está.
 */
export function proximoNaoDecidido(
  itens: readonly ItemRevisao[],
  decisoes: Readonly<Record<string, Resultado>>,
  atualId: string
): string {
  const pendente = (item: ItemRevisao) => !decisoes[item.publication.id]
  const posicao = itens.findIndex((item) => item.publication.id === atualId)
  const depois = itens.slice(posicao + 1).find(pendente)
  const qualquer = itens.find((item) => pendente(item) && item.publication.id !== atualId)

  return (depois ?? qualquer)?.publication.id ?? atualId
}

export const rotuloDecisao: Record<Decisao, string> = {
  accepted: 'Prazo aceito e salvo na agenda',
  corrected: 'Prazo corrigido e salvo',
  rejected: 'Sugestão rejeitada',
}

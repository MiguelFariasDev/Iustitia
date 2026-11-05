import { queryOptions } from '@tanstack/react-query'
import type { AxiosInstance } from 'axios'
import { queryKeys } from '@/lib/query/queryKeys'
import {
  DEFAULT_ATENDIMENTOS_PAGE_SIZE,
  getAtendimento,
  getAtendimentos,
  getMensagens,
} from './api'
import type { AtendimentosFilters } from './types'

/** Normaliza para a chave não mudar por diferença irrelevante (undefined vs null vs ''). */
export function normalizeAtendimentosFilters(filters: AtendimentosFilters) {
  return {
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? DEFAULT_ATENDIMENTOS_PAGE_SIZE,
    status: filters.status ?? null,
    assignedUserId: filters.assignedUserId ?? null,
    search: filters.search?.trim() || null,
  }
}

/**
 * `refetchInterval` é a rede de segurança do Realtime: se o canal cair ou nunca conectar, a fila
 * ainda converge em meio minuto. Com o canal ativo, o push invalida antes e o intervalo nunca vale.
 */
export const atendimentosQuery = (filters: AtendimentosFilters = {}, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.atendimentos.list(normalizeAtendimentosFilters(filters)),
    queryFn: () => getAtendimentos(filters, client),
    refetchInterval: 30_000,
  })

export const atendimentoQuery = (id: string, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.atendimentos.byId(id),
    queryFn: () => getAtendimento(id, client),
  })

/** Mensagens da conversa aberta — o Realtime só avisa; o conteúdo vem sempre por aqui. */
export const mensagensQuery = (id: string, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.atendimentos.mensagens(id),
    queryFn: () => getMensagens(id, null, client),
    refetchInterval: 15_000,
  })

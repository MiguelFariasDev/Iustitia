import type { AxiosInstance } from 'axios'
import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { mockResponse } from '@/lib/api/mock'
import {
  toAtendimento,
  toMensagem,
  type AtendimentoDto,
  type AtendimentosPageDto,
  type MensagensPageDto,
  type SendMessageDto,
} from './contract'
import {
  mockChangeStatus,
  mockGetAtendimento,
  mockLinkCliente,
  mockListAtendimentos,
  mockListMensagens,
  mockSendMensagem,
} from './mocks'
import type {
  Atendimento,
  AtendimentosFilters,
  AtendimentosPage,
  MensagemEntrega,
  MensagensPage,
  StatusAlvo,
} from './types'

export const DEFAULT_ATENDIMENTOS_PAGE_SIZE = 30
export const DEFAULT_MENSAGENS_LIMIT = 50

export async function getAtendimentos(
  filters: AtendimentosFilters = {},
  client: AxiosInstance | undefined = apiClient
): Promise<AtendimentosPage> {
  const normalized = { ...filters, pageSize: filters.pageSize ?? DEFAULT_ATENDIMENTOS_PAGE_SIZE }
  if (env.useMocks || !client) return mockResponse(mockListAtendimentos(normalized), 200)

  const { data } = await client.get<AtendimentosPageDto>(endpoints.atendimentos.list, {
    params: {
      page: normalized.page ?? 1,
      pageSize: normalized.pageSize,
      status: normalized.status ?? undefined,
      assignedUserId: normalized.assignedUserId ?? undefined,
      search: normalized.search?.trim() || undefined,
    },
  })

  return { ...data, items: data.items.map(toAtendimento) }
}

export async function getAtendimento(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<Atendimento> {
  if (env.useMocks || !client) return mockResponse(mockGetAtendimento(id), 120)

  const { data } = await client.get<AtendimentoDto>(endpoints.atendimentos.byId(id))
  return toAtendimento(data)
}

/** `before` (ISO) carrega as mensagens anteriores a esse instante. */
export async function getMensagens(
  id: string,
  before?: string | null,
  client: AxiosInstance | undefined = apiClient
): Promise<MensagensPage> {
  if (env.useMocks || !client) {
    return mockResponse(mockListMensagens(id, before, DEFAULT_MENSAGENS_LIMIT), 150)
  }

  const { data } = await client.get<MensagensPageDto>(endpoints.atendimentos.mensagens(id), {
    params: { before: before ?? undefined, limit: DEFAULT_MENSAGENS_LIMIT },
  })

  return { items: data.items.map(toMensagem), hasMore: data.hasMore }
}

export interface SendResult {
  messageId: string
  delivery: MensagemEntrega
}

export async function sendMensagem(
  id: string,
  body: string,
  client: AxiosInstance | undefined = apiClient
): Promise<SendResult> {
  if (env.useMocks || !client) return mockResponse(mockSendMensagem(id, body), 250)

  const { data } = await client.post<SendMessageDto>(endpoints.atendimentos.mensagens(id), { body })
  return { messageId: data.messageId, delivery: data.delivery === 'Failed' ? 'Failed' : 'Sent' }
}

/** Human = assumir; Bot = devolver ao bot; Closed = encerrar. */
export async function changeStatus(
  id: string,
  status: StatusAlvo,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (env.useMocks || !client) {
    mockChangeStatus(id, status)
    return mockResponse(undefined, 200)
  }

  await client.patch(endpoints.atendimentos.status(id), { status })
}

export async function linkCliente(
  id: string,
  clienteId: string,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (env.useMocks || !client) {
    mockLinkCliente(id, clienteId)
    return mockResponse(undefined, 150)
  }

  await client.put(endpoints.atendimentos.cliente(id), { clienteId })
}

import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import { toContactPayload, toContato, type ContactDto, type ListContactsDto } from './contract'
import {
  mockCreateContato,
  mockDeleteContato,
  mockGetContato,
  mockListContatos,
  mockUpdateContato,
} from './mocks'
import type { ContatoFormValues } from './schemas'
import type { Contato, ContatosFilters } from './types'

/**
 * Com `NEXT_PUBLIC_API_MOCKS` ligado (padrão em desenvolvimento sem backend) usa o repositório
 * em memória; desligado, fala com `/contatos` (CRM.Api).
 *
 * A listagem pede um tamanho de página alto e não pagina na tela: um escritório pequeno tem
 * centenas de contatos, não milhares, e a tela filtra por tipo no cliente.
 */
const LIST_PAGE_SIZE = 100

const notFound = () =>
  new ApiError(404, { errorCode: 'CONTACT_NOT_FOUND', detail: 'Contato não encontrado.' })

export async function listContatos(filters: ContatosFilters = {}): Promise<Contato[]> {
  if (env.useMocks) return mockResponse(mockListContatos(filters), 250)

  const { data } = await apiClient.get<ListContactsDto>(endpoints.contatos.list, {
    params: {
      page: 1,
      pageSize: LIST_PAGE_SIZE,
      tipo: filters.tipo ?? undefined,
      apenasAtivos: true,
    },
  })

  return data.items.map(toContato)
}

export async function getContatoById(id: string): Promise<Contato> {
  if (env.useMocks) {
    const found = mockGetContato(id)
    if (!found) throw notFound()

    return mockResponse(found, 150)
  }

  const { data } = await apiClient.get<ContactDto>(endpoints.contatos.byId(id))

  return toContato(data)
}

export async function createContato(values: ContatoFormValues): Promise<Contato> {
  if (env.useMocks) return mockResponse(mockCreateContato(values), 200)

  const { data } = await apiClient.post<ContactDto>(
    endpoints.contatos.list,
    toContactPayload(values)
  )

  return toContato(data)
}

export async function updateContato(id: string, values: ContatoFormValues): Promise<Contato> {
  if (env.useMocks) {
    const updated = mockUpdateContato(id, values)
    if (!updated) throw notFound()

    return mockResponse(updated, 200)
  }

  await apiClient.put(endpoints.contatos.byId(id), toContactPayload(values))

  return getContatoById(id)
}

/** No servidor isto DESATIVA o contato (não existe exclusão física — ver ADR-067). */
export async function deleteContato(id: string): Promise<void> {
  if (env.useMocks) {
    if (!mockDeleteContato(id)) throw notFound()

    await mockResponse(null, 150)
    return
  }

  await apiClient.delete(endpoints.contatos.byId(id))
}

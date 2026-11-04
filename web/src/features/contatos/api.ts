import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
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
 * Contatos ainda não têm endpoint: TODAS as chamadas usam o repositório em memória
 * (mocks.ts), independentemente de NEXT_PUBLIC_API_MOCKS — ligar a API real hoje daria 404.
 * A tela avisa que os dados são de demonstração.
 *
 * TODO(contatos-backend): ao existir `/contatos`, trocar o corpo de cada função por uma chamada
 * ao `apiClient` (como clientes/api.ts) e remover o aviso de demonstração da tela.
 */
const notFound = () =>
  new ApiError(404, { errorCode: 'CONTATO_NOT_FOUND', detail: 'Contato não encontrado.' })

export function listContatos(filters: ContatosFilters = {}): Promise<Contato[]> {
  return mockResponse(mockListContatos(filters), 250)
}

export async function getContatoById(id: string): Promise<Contato> {
  const found = mockGetContato(id)
  if (!found) throw notFound()

  return mockResponse(found, 150)
}

export function createContato(values: ContatoFormValues): Promise<Contato> {
  return mockResponse(mockCreateContato(values), 200)
}

export async function updateContato(id: string, values: ContatoFormValues): Promise<Contato> {
  const updated = mockUpdateContato(id, values)
  if (!updated) throw notFound()

  return mockResponse(updated, 200)
}

export async function deleteContato(id: string): Promise<void> {
  if (!mockDeleteContato(id)) throw notFound()

  await mockResponse(null, 150)
}

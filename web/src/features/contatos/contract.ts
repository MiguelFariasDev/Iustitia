import type { Contato, TipoContato } from './types'

/**
 * Camada anticorrupção entre CRM.Api e o modelo da tela (mesma razão de clientes/contract.ts).
 * O backend usa nomes em inglês; a tela, em português.
 */

/** ContactResponse (CRM.Application.Features.Contacts). Documento e telefone já saem mascarados. */
export interface ContactDto {
  id: string
  type: string
  name: string
  document: string | null
  email: string | null
  phone: string | null
  notes: string | null
  isActive: boolean
  createdAt: string
}

export interface ListContactsDto {
  items: ContactDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

const tipos: readonly TipoContato[] = ['ParteContraria', 'Testemunha', 'Fornecedor', 'Outros']

/** Valor desconhecido cai em "Outros" em vez de quebrar a listagem inteira. */
function toTipo(value: string): TipoContato {
  return tipos.find((tipo) => tipo === value) ?? 'Outros'
}

export function toContato(dto: ContactDto): Contato {
  return {
    id: dto.id,
    nome: dto.name,
    tipo: toTipo(dto.type),
    documento: dto.document,
    email: dto.email,
    telefone: dto.phone,
    observacoes: dto.notes,
    createdAt: dto.createdAt,
  }
}

/** Campos vazios do formulário viram `null` — o backend trata '' e ausente do mesmo jeito, mas `null` é explícito. */
export function toContactPayload(values: {
  nome: string
  tipo: TipoContato
  documento: string
  email: string
  telefone: string
  observacoes: string
}) {
  const blankToNull = (value: string) => (value.trim() === '' ? null : value.trim())

  return {
    type: values.tipo,
    name: values.nome.trim(),
    document: blankToNull(values.documento),
    email: blankToNull(values.email),
    phone: blankToNull(values.telefone),
    notes: blankToNull(values.observacoes),
  }
}

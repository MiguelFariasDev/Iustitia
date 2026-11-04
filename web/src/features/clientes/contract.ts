import type { Cliente, ClienteListItem, TipoCliente } from './types'

/**
 * Camada anticorrupção entre Legal.Api e o modelo da tela — mesma razão de
 * features/publicacoes/contract.ts.
 *
 * Aqui a distância é pequena (os nomes coincidem), mas a camada existe para que uma mudança
 * no contrato da API não se espalhe por todos os componentes.
 */

/** ClienteResponse (Legal.Application.Features.Clientes). */
export interface ClienteDto {
  id: string
  tipo: string
  nome: string
  documento: string
  email: string | null
  telefone: string | null
  logradouro: string | null
  numero: string | null
  complemento: string | null
  bairro: string | null
  cidade: string | null
  uf: string | null
  cep: string | null
  dataNascimento: string | null
  observacoes: string | null
  isActive: boolean
  createdAt: string
}

/** ClienteListItem (Legal.Application.Features.Clientes). */
export interface ClienteListItemDto {
  id: string
  tipo: string
  nome: string
  documento: string
  email: string | null
  telefone: string | null
  isActive: boolean
}

export interface ListClientesDto {
  items: ClienteListItemDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/**
 * Tipo desconhecido cai em pessoa física. É o caso majoritário num escritório e, ao
 * contrário de um papel de acesso, não concede nada — errar aqui muda um rótulo na tela,
 * não uma permissão.
 */
function toTipo(apiTipo: string): TipoCliente {
  return apiTipo === 'PessoaJuridica' ? 'PessoaJuridica' : 'PessoaFisica'
}

export function toCliente(dto: ClienteDto): Cliente {
  return {
    id: dto.id,
    tipo: toTipo(dto.tipo),
    nome: dto.nome,
    documento: dto.documento,
    email: dto.email,
    telefone: dto.telefone,
    dataNascimento: dto.dataNascimento,
    observacoes: dto.observacoes,
    isActive: dto.isActive,
    createdAt: dto.createdAt,
    endereco: {
      logradouro: dto.logradouro,
      numero: dto.numero,
      complemento: dto.complemento,
      bairro: dto.bairro,
      cidade: dto.cidade,
      uf: dto.uf,
      cep: dto.cep,
    },
  }
}

export function toClienteListItem(dto: ClienteListItemDto): ClienteListItem {
  return {
    id: dto.id,
    tipo: toTipo(dto.tipo),
    nome: dto.nome,
    documento: dto.documento,
    email: dto.email,
    telefone: dto.telefone,
    isActive: dto.isActive,
  }
}

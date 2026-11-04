/** Modelo de exibição do cliente — o contrato da API é traduzido em contract.ts. */

export type TipoCliente = 'PessoaFisica' | 'PessoaJuridica'

export interface Cliente {
  id: string
  tipo: TipoCliente
  nome: string
  /** Já mascarado pela API (000.000.000-00 ou 00.000.000/0000-00). */
  documento: string
  email: string | null
  /** Já mascarado pela API ((00) 00000-0000). */
  telefone: string | null
  dataNascimento: string | null
  observacoes: string | null
  isActive: boolean
  createdAt: string
  endereco: EnderecoCliente
}

export interface EnderecoCliente {
  logradouro: string | null
  numero: string | null
  complemento: string | null
  bairro: string | null
  cidade: string | null
  uf: string | null
  cep: string | null
}

/** Item de listagem — sem endereço nem observações, que só aparecem no detalhe. */
export interface ClienteListItem {
  id: string
  tipo: TipoCliente
  nome: string
  documento: string
  email: string | null
  telefone: string | null
  isActive: boolean
}

export interface ClientesPage {
  items: ClienteListItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ClientesFilters {
  page?: number
  pageSize?: number
  tipo?: TipoCliente | null
  apenasAtivos?: boolean | null
  busca?: string
}

export const tipoClienteLabels: Record<TipoCliente, string> = {
  PessoaFisica: 'Pessoa física',
  PessoaJuridica: 'Pessoa jurídica',
}

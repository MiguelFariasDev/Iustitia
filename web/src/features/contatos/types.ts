/** Modelo de exibição do contato. Contato NÃO é cliente: não responde por processos. */

export type TipoContato = 'ParteContraria' | 'Testemunha' | 'Fornecedor' | 'Outros'

export const tiposContato: readonly TipoContato[] = [
  'ParteContraria',
  'Testemunha',
  'Fornecedor',
  'Outros',
]

export const tipoContatoLabels: Record<TipoContato, string> = {
  ParteContraria: 'Parte contrária',
  Testemunha: 'Testemunha',
  Fornecedor: 'Fornecedor',
  Outros: 'Outros',
}

export interface Contato {
  id: string
  nome: string
  tipo: TipoContato
  /** CPF ou CNPJ já mascarado; vazio quando não informado. */
  documento: string | null
  email: string | null
  telefone: string | null
  observacoes: string | null
  createdAt: string
}

export interface ContatosFilters {
  tipo?: TipoContato | null
}

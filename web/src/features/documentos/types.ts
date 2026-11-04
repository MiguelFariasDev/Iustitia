export type TipoArquivo = 'PDF' | 'DOCX' | 'Imagem' | 'Outro'
export type TipoVinculo = 'Processo' | 'Cliente' | 'Avulso'

export const tiposArquivo: readonly TipoArquivo[] = ['PDF', 'DOCX', 'Imagem']
export const tiposVinculo: readonly TipoVinculo[] = ['Processo', 'Cliente', 'Avulso']

export const tipoVinculoLabels: Record<TipoVinculo, string> = {
  Processo: 'Processo',
  Cliente: 'Cliente',
  Avulso: 'Avulso',
}

export interface Documento {
  id: string
  nome: string
  tipoArquivo: TipoArquivo
  tamanhoBytes: number
  criadoEm: string
  vinculo: {
    tipo: TipoVinculo
    /** Número do processo ou nome do cliente; `null` para documento avulso. */
    rotulo: string | null
  }
}

export interface DocumentosFilters {
  busca?: string
  tipoArquivo?: TipoArquivo | null
  vinculo?: TipoVinculo | null
}

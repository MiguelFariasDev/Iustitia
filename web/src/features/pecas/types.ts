/**
 * Valores aceitos pela API de geração (PecaCatalogo, no backend). O backend recusa texto livre
 * em `tipoPeca` e `area`, então a tela só oferece estas chaves.
 */
export const tiposPeca = [
  { value: 'peticao_inicial', label: 'Petição inicial' },
  { value: 'contestacao', label: 'Contestação' },
  { value: 'replica', label: 'Réplica' },
  { value: 'recurso_apelacao', label: 'Recurso de apelação' },
  { value: 'embargos_declaracao', label: 'Embargos de declaração' },
  { value: 'agravo_instrumento', label: 'Agravo de instrumento' },
  { value: 'contrarrazoes', label: 'Contrarrazões' },
  { value: 'manifestacao', label: 'Manifestação' },
] as const

export const areasPeca = [
  { value: 'civel', label: 'Cível' },
  { value: 'trabalhista', label: 'Trabalhista' },
  { value: 'previdenciario', label: 'Previdenciário' },
  { value: 'tributario', label: 'Tributário' },
  { value: 'familia', label: 'Família' },
  { value: 'consumidor', label: 'Consumidor' },
  { value: 'administrativo', label: 'Administrativo' },
  { value: 'empresarial', label: 'Empresarial' },
] as const

/** Tipos de documento aceitos no upload do acervo; os que fazem sentido para anexos de um processo. */
export const tiposAnexo = [
  { value: 'peticao', label: 'Petição' },
  { value: 'sentenca', label: 'Sentença' },
  { value: 'acordao', label: 'Acórdão' },
  { value: 'contrato', label: 'Contrato' },
  { value: 'parecer', label: 'Parecer' },
  { value: 'outro', label: 'Outro documento' },
] as const

export type TipoPeca = (typeof tiposPeca)[number]['value']
export type AreaPeca = (typeof areasPeca)[number]['value']
export type TipoAnexo = (typeof tiposAnexo)[number]['value']

/** Extensões que o backend extrai (PDF com texto, DOCX, TXT). PDF escaneado é recusado: não há OCR. */
export const EXTENSOES_ANEXO = '.pdf,.docx,.txt'

export interface GerarPecaRequest {
  tipoPeca: TipoPeca
  area: AreaPeca
  fatos: string
  pedidos?: string
  /** O comando do advogado: instruções livres de tom, tese e estrutura. */
  instrucoesAdicionais?: string
  /** Ids de documentos JÁ indexados no acervo. */
  documentosIds?: string[]
}

export interface Submissao {
  id: string
  status: string
  mensagem: string
}

export interface StatusPeca {
  id: string
  status: string
  etapa: string | null
  progresso: number
  mensagem: string
  erro: string | null
}

export interface FontePeca {
  id: string
  nomeArquivo: string
  pagina: number | null
  trecho: string
  citada: boolean
  urlOrigem: string | null
}

export interface PecaDetalhe {
  id: string
  status: string
  tipoPeca: string
  area: string
  versao: number
  textoGerado: string | null
  fontes: FontePeca[]
  avisos: string[]
  aviso: string
  exportacaoBloqueada: boolean
}

export interface DocumentoAcervo {
  id: string
  nomeArquivo: string
  status: string
}

/** Estados de uma solicitação que não mudam mais — o polling para aqui. */
export function statusTerminal(status: string | undefined): boolean {
  const normalizado = (status ?? '')
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()

  return normalizado === 'concluido' || normalizado === 'falhou' || normalizado === 'cancelado'
}

export function statusIs(status: string | undefined, esperado: string): boolean {
  const limpar = (valor: string) =>
    valor
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLowerCase()

  return limpar(status ?? '') === limpar(esperado)
}

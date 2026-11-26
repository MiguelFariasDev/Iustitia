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
  { value: 'recurso_ordinario', label: 'Recurso ordinário' },
  { value: 'recurso_especial', label: 'Recurso especial' },
  { value: 'recurso_extraordinario', label: 'Recurso extraordinário' },
  { value: 'contrato', label: 'Contrato' },
  { value: 'parecer', label: 'Parecer' },
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
  { value: 'penal', label: 'Penal' },
  { value: 'constitucional', label: 'Constitucional' },
] as const

export type TipoPeca = (typeof tiposPeca)[number]['value']
export type AreaPeca = (typeof areasPeca)[number]['value']

/** Extensões que o backend lê. Escaneados e imagens passam por OCR; áudio, por transcrição. */
export const EXTENSOES_ANEXO =
  '.pdf,.docx,.doc,.txt,.md,.png,.jpg,.jpeg,.tif,.tiff,.xlsx,.xls,.ods,.csv,.eml,.mp3,.wav,.m4a,.ogg,.opus,.flac'

export interface GerarPecaRequest {
  /** Obrigatório: a IA só enxerga os anexos deste caso. */
  casoId: string
  tipoPeca: TipoPeca
  area: AreaPeca
  fatos: string
  pedidos?: string
  /** O comando do advogado: instruções livres de tom, tese e estrutura. */
  instrucoesAdicionais?: string
  /** Ids de anexos do caso, para restringir a busca. Ausente = todos os anexos do caso. */
  documentosIds?: string[]
  /** AAAA-MM-DD: base da conferência de vigência das normas citadas. */
  dataFatos?: string
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
  rotulo?: string | null
  situacao?: string | null
  urn?: string | null
}

export type StatusCitacao =
  'Validada' | 'PendenteDeVerificacao' | 'PossivelAlucinacao' | 'ConflitoDeVersao' | 'NormaRevogada'

export type DecisaoCitacao = 'pendente' | 'confirmada' | 'corrigida' | 'rejeitada'

export interface CitacaoPeca {
  id: string
  texto: string
  status: StatusCitacao
  detalhe: string
  bloqueante: boolean
  fonteUrl: string | null
  decisao: DecisaoCitacao
  justificativa: string | null
}

export const rotuloStatusCitacao: Record<StatusCitacao, string> = {
  Validada: 'Validada',
  PendenteDeVerificacao: 'Pendente de verificação',
  PossivelAlucinacao: 'Possível invenção da IA',
  ConflitoDeVersao: 'Conflito de versão',
  NormaRevogada: 'Norma revogada',
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
  citacoes: CitacaoPeca[] | null
  revisadoEm: string | null
  textoRevisado: string | null
}

export interface Caso {
  id: string
  clienteId: string
  clienteNome: string | null
  titulo: string
  area: string
  processoId: string | null
  status: string
  podeEscrever: boolean
}

export interface CriarCasoRequest {
  clienteId: string
  titulo: string
  area: AreaPeca
  clienteNome?: string
  processoId?: string
  numeroProcesso?: string
}

export interface AnexoCaso {
  id: string
  nomeArquivo: string
  status: string
  mensagem: string | null
  origem: string
  confiancaOcr: string | null
  tipoDocumento: string | null
  requerRevisaoHumana: boolean
  revisado: boolean
  conteudoSuspeito: boolean
}

/** Estados de um anexo que não mudam mais sozinhos. */
export function anexoTerminado(status: string): boolean {
  return status === 'Indexado' || status === 'Falhou'
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

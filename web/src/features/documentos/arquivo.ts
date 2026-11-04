import type { TipoArquivo } from './types'

/** Limite por arquivo. Espelha o que o storage vai aceitar; o backend revalida. */
export const TAMANHO_MAXIMO_BYTES = 10 * 1024 * 1024

/** Extensões aceitas no upload, por tipo. */
const extensoes: Record<Exclude<TipoArquivo, 'Outro'>, readonly string[]> = {
  PDF: ['pdf'],
  DOCX: ['docx'],
  Imagem: ['png', 'jpg', 'jpeg'],
}

export const ACCEPT_UPLOAD = Object.values(extensoes)
  .flat()
  .map((e) => `.${e}`)
  .join(',')

export function extensaoDe(nome: string): string {
  const i = nome.lastIndexOf('.')

  return i < 0 ? '' : nome.slice(i + 1).toLowerCase()
}

/** O tipo vem da extensão: o `type` MIME que o navegador informa é vazio em vários sistemas. */
export function detectarTipoArquivo(nome: string): TipoArquivo {
  const ext = extensaoDe(nome)
  const achado = (Object.entries(extensoes) as [TipoArquivo, readonly string[]][]).find(
    ([, lista]) => lista.includes(ext)
  )

  return achado ? achado[0] : 'Outro'
}

export function formatTamanho(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`

  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`
}

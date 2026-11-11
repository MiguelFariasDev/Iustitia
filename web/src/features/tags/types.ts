/** Onde uma tag pode ser colocada: no processo ou no advogado responsável por ele. */
export type AlvoTag = 'processo' | 'advogado'

export interface Tag {
  id: string
  nome: string
  /** Cor em hexadecimal (#RRGGBB). */
  cor: string
}

export const coresTag = [
  '#2563eb',
  '#0d9488',
  '#16a34a',
  '#ca8a04',
  '#ea580c',
  '#dc2626',
  '#9333ea',
  '#64748b',
] as const

export interface EstadoTags {
  tags: Tag[]
  /** `${alvo}:${id}` → ids das tags aplicadas. */
  atribuicoes: Record<string, string[]>
}

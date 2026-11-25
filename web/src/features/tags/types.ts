/** Onde uma etiqueta pode ser colocada: no processo ou no advogado responsável por ele. */
export type AlvoTag = 'processo' | 'advogado'

export interface Tag {
  id: string
  nome: string
  /** Cor em hexadecimal (#RRGGBB). */
  cor: string
}

/**
 * Cores das etiquetas, uma por etiqueta. Tons escuros o bastante para o texto ler bem sobre o
 * fundo claro do próprio selo (o selo usa a cor com 10% de opacidade como fundo).
 */
export const coresTag = [
  '#2563eb', // azul
  '#dc2626', // vermelho
  '#16a34a', // verde
  '#9333ea', // roxo
  '#ea580c', // laranja
  '#0d9488', // verde-azulado
  '#db2777', // rosa
  '#ca8a04', // amarelo-escuro
  '#4f46e5', // índigo
  '#0891b2', // ciano
  '#65a30d', // verde-limão
  '#b45309', // âmbar
] as const

/** Cor que o servidor grava quando a etiqueta nasce sem cor (Tag.DefaultColor). */
export const COR_PADRAO_SERVIDOR = '#64748b'

/** Primeira cor da paleta que nenhuma etiqueta usa; com todas em uso, recomeça pela ordem. */
export function proximaCor(usadas: readonly string[]): string {
  const emUso = new Set(usadas.map((cor) => cor.toLowerCase()))

  return coresTag.find((cor) => !emUso.has(cor)) ?? coresTag[usadas.length % coresTag.length]!
}

/**
 * Etiquetas antigas foram gravadas sem cor (todas cinza). Na exibição, cada uma recebe uma cor da
 * paleta ainda não usada, para que etiquetas diferentes nunca pareçam iguais.
 */
export function colorirEtiquetas(tags: readonly Tag[]): Tag[] {
  const usadas = tags.map((tag) => tag.cor.toLowerCase()).filter((cor) => cor !== COR_PADRAO_SERVIDOR)

  return tags.map((tag) => {
    if (tag.cor.toLowerCase() !== COR_PADRAO_SERVIDOR) return tag
    const cor = proximaCor(usadas)
    usadas.push(cor)

    return { ...tag, cor }
  })
}

export interface EstadoTags {
  tags: Tag[]
  /** `${alvo}:${id}` → ids das tags aplicadas. */
  atribuicoes: Record<string, string[]>
}

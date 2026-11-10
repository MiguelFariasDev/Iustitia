/** Um pedaço do parágrafo: texto comum ou uma citação legal (destacada na tela). */
export interface Trecho {
  texto: string
  citacao: boolean
}

/**
 * "art. 335 do CPC", "art. 1.010, § 1º, do CPC", "arts. 337 e 341 do CPC", "art. 85, § 11, do CPC".
 * Só reconhece o que tem código reconhecível no fim (CPC, CF, CC, CLT, CDC): "art. 5º" solto num
 * texto livre não é destacado, para não marcar como lei o que pode ser outra coisa.
 */
const CITACAO =
  /\barts?\.\s*\d[\d.]*º?(?:,\s*§\s*\d+º?)?(?:\s*(?:e|a)\s*\d[\d.]*º?)?,?\s*(?:do|da)\s+(?:CPC|CF|CC|CLT|CDC)\b/gi

export function separarCitacoes(paragrafo: string): Trecho[] {
  const trechos: Trecho[] = []
  let cursor = 0

  for (const achado of paragrafo.matchAll(CITACAO)) {
    if (achado.index > cursor) {
      trechos.push({ texto: paragrafo.slice(cursor, achado.index), citacao: false })
    }
    trechos.push({ texto: achado[0], citacao: true })
    cursor = achado.index + achado[0].length
  }

  if (cursor < paragrafo.length) {
    trechos.push({ texto: paragrafo.slice(cursor), citacao: false })
  }

  return trechos
}

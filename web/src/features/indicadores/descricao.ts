import { formatNumber } from '@/lib/format'
import type { ItemSerie } from './types'

/**
 * Texto que descreve um gráfico para leitores de tela: "Título: A 31, B 24…". É o que vai em
 * `aria-label` — o gráfico é uma imagem para a tecnologia assistiva, então os números
 * precisam estar ditos em algum lugar. Sem dados, diz isso em vez de calar.
 */
export function descreverSerie(titulo: string, itens: ItemSerie[], unidade = ''): string {
  if (itens.length === 0) return `${titulo}: sem dados no período.`

  const partes = itens.map((i) => `${i.rotulo} ${formatNumber(i.total)}${unidade}`)

  return `${titulo}: ${partes.join(', ')}.`
}

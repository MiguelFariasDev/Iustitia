import { UNIDADES_FEDERACAO } from '@/lib/validation/documento'

/**
 * Siglas que o backend deriva do número CNJ (Legal.Domain.Shared.TribunalResolver). As UFs vêm da mesma
 * tabela que a leitura do número CNJ (UNIDADES_FEDERACAO), para as duas nunca divergirem. Lista
 * fixa: o conjunto de tribunais brasileiros é estável e um filtro por sigla não deve depender
 * de uma consulta ao banco para montar as opções.
 */
export const tribunais: string[] = [
  'STF',
  'STJ',
  ...Array.from({ length: 6 }, (_, i) => `TRF${i + 1}`),
  ...Array.from({ length: 24 }, (_, i) => `TRT${i + 1}`),
  ...UNIDADES_FEDERACAO.map((uf) => `TJ${uf}`),
]

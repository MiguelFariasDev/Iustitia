/**
 * Siglas que o backend deriva do número CNJ (Legal.Domain.Shared.TribunalResolver). Lista
 * fixa: o conjunto de tribunais brasileiros é estável e um filtro por sigla não deve depender
 * de uma consulta ao banco para montar as opções.
 */
const ufsEstaduais = [
  'AC',
  'AL',
  'AP',
  'AM',
  'BA',
  'CE',
  'DFT',
  'ES',
  'GO',
  'MA',
  'MT',
  'MS',
  'MG',
  'PA',
  'PB',
  'PR',
  'PE',
  'PI',
  'RJ',
  'RN',
  'RS',
  'RO',
  'RR',
  'SC',
  'SE',
  'SP',
  'TO',
] as const

export const tribunais: string[] = [
  'STF',
  'STJ',
  ...Array.from({ length: 6 }, (_, i) => `TRF${i + 1}`),
  ...Array.from({ length: 24 }, (_, i) => `TRT${i + 1}`),
  ...ufsEstaduais.map((uf) => `TJ${uf}`),
]

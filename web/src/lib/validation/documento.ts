/**
 * Validação e máscara de CPF, telefone, CEP e número CNJ.
 *
 * Espelha as regras do backend (Legal.Domain: Documento, Contato, CNJNumber) para que o
 * formulário avise antes de enviar. O backend continua validando: esta camada é conveniência
 * de UX, nunca a fronteira de confiança.
 */

/** Valida CPF (11 dígitos + dígitos verificadores). */
export function isValidCpf(value: string): boolean {
  const digits = value.replace(/\D/g, '')
  // Sequência de dígitos iguais passa na aritmética mas não é documento de ninguém.
  if (digits.length !== 11 || /^(\d)\1{10}$/.test(digits)) return false

  const calc = (length: number) => {
    let sum = 0
    for (let i = 0; i < length; i++) sum += Number(digits[i]) * (length + 1 - i)
    const rest = sum % 11
    return rest < 2 ? 0 : 11 - rest
  }

  return calc(9) === Number(digits[9]) && calc(10) === Number(digits[10])
}

export function formatCpf(value: string) {
  const d = value.replace(/\D/g, '').slice(0, 11)
  return d
    .replace(/^(\d{3})(\d)/, '$1.$2')
    .replace(/^(\d{3})\.(\d{3})(\d)/, '$1.$2.$3')
    .replace(/\.(\d{3})(\d)/, '.$1-$2')
}

/** Telefone brasileiro: 10 dígitos (fixo) ou 11 (celular), sempre com DDD. */
export function isValidTelefone(value: string): boolean {
  const digits = value.replace(/\D/g, '')
  return digits.length === 10 || digits.length === 11
}

export function formatTelefone(value: string) {
  const d = value.replace(/\D/g, '').slice(0, 11)
  if (d.length <= 10) {
    return d.replace(/^(\d{2})(\d)/, '($1) $2').replace(/(\d{4})(\d)/, '$1-$2')
  }
  return d.replace(/^(\d{2})(\d)/, '($1) $2').replace(/(\d{5})(\d)/, '$1-$2')
}

export function formatCep(value: string) {
  return value
    .replace(/\D/g, '')
    .slice(0, 8)
    .replace(/^(\d{5})(\d)/, '$1-$2')
}

export function isValidCep(value: string): boolean {
  return value.replace(/\D/g, '').length === 8
}

/**
 * Valida o número CNJ (Resolução CNJ nº 65/2008): 20 dígitos com verificação mod 97
 * (ISO 7064). Mesma regra de Legal.Domain.Shared.CNJNumber — um número com dígito
 * verificador errado é dado corrompido, e aceitá-lo aqui contaminaria a captura.
 *
 * BigInt porque o número reordenado tem 20 dígitos e não cabe em Number com precisão.
 */
export function isValidCnj(value: string): boolean {
  const digits = value.replace(/\D/g, '')
  if (digits.length !== 20) return false

  // Segmento do Judiciário (posição 13) vai de 1 a 9.
  if (digits[13] === '0') return false

  const checkDigits = Number(digits.slice(7, 9))
  const withoutCheckDigits = `${digits.slice(0, 7)}${digits.slice(9)}00`
  const remainder = Number(BigInt(withoutCheckDigits) % 97n)

  return 98 - remainder === checkDigits
}

/** NNNNNNN-DD.AAAA.J.TR.OOOO */
export function formatCnj(value: string) {
  const d = value.replace(/\D/g, '').slice(0, 20)
  return d
    .replace(/^(\d{7})(\d)/, '$1-$2')
    .replace(/^(\d{7})-(\d{2})(\d)/, '$1-$2.$3')
    .replace(/^(\d{7})-(\d{2})\.(\d{4})(\d)/, '$1-$2.$3.$4')
    .replace(/^(\d{7})-(\d{2})\.(\d{4})\.(\d)(\d)/, '$1-$2.$3.$4.$5')
    .replace(/^(\d{7})-(\d{2})\.(\d{4})\.(\d)\.(\d{2})(\d)/, '$1-$2.$3.$4.$5.$6')
}

export function onlyDigits(value: string) {
  return value.replace(/\D/g, '')
}

/** UFs na ordem dos códigos TR 01..27 da Resolução CNJ nº 65/2008. */
const UNIDADES_FEDERACAO = [
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

const MILITARES_ESTADUAIS: Record<number, string> = { 13: 'TJMMG', 21: 'TJMRS', 26: 'TJMSP' }

/**
 * Sigla do tribunal codificada no próprio número CNJ (campos J e TR).
 * Espelha TribunalResolver no backend — os dois têm os mesmos casos de teste.
 *
 * Serve para a tela dizer "consultar no TJSP" antes de chamar a API, e para desabilitar o
 * botão quando o número não aponta para tribunal nenhum.
 */
export function getTribunalFromCnj(value: string): string | null {
  const digits = onlyDigits(value)
  if (digits.length !== 20) return null

  const segmento = Number(digits[13])
  const codigo = Number(digits.slice(14, 16))
  const uf =
    codigo >= 1 && codigo <= UNIDADES_FEDERACAO.length ? UNIDADES_FEDERACAO[codigo - 1] : null

  switch (segmento) {
    case 1:
      return 'STF'
    case 2:
      return 'CNJ'
    case 3:
      return 'STJ'
    case 4:
      return codigo >= 1 && codigo <= 6 ? `TRF${codigo}` : null
    case 5:
      return codigo >= 1 && codigo <= 24 ? `TRT${codigo}` : null
    case 6:
      return codigo === 0 ? 'TSE' : uf ? `TRE-${uf}` : null
    case 7:
      return 'STM'
    case 8:
      return uf ? `TJ${uf}` : null
    case 9:
      return MILITARES_ESTADUAIS[codigo] ?? null
    default:
      return null
  }
}

/**
 * Tribunais que a consulta pública do DataJud atende. O STF não publica por lá, então um
 * processo do STF tem número válido e consulta impossível — melhor a tela saber disso antes
 * de gastar uma chamada e devolver erro.
 */
const TRIBUNAIS_NO_DATAJUD = new Set([
  'TST',
  'TSE',
  'STJ',
  'STM',
  'TRF1',
  'TRF2',
  'TRF3',
  'TRF4',
  'TRF5',
  'TRF6',
  'TJAC',
  'TJAL',
  'TJAP',
  'TJAM',
  'TJBA',
  'TJCE',
  'TJDFT',
  'TJES',
  'TJGO',
  'TJMA',
  'TJMT',
  'TJMS',
  'TJMG',
  'TJPA',
  'TJPB',
  'TJPR',
  'TJPE',
  'TJPI',
  'TJRJ',
  'TJRN',
  'TJRS',
  'TJRO',
  'TJRR',
  'TJSC',
  'TJSE',
  'TJSP',
  'TJTO',
  ...Array.from({ length: 24 }, (_, i) => `TRT${i + 1}`),
  ...UNIDADES_FEDERACAO.map((uf) => `TRE-${uf}`),
  'TJMMG',
  'TJMRS',
  'TJMSP',
])

export function tribunalTemConsultaPublica(tribunal: string | null): boolean {
  return tribunal !== null && TRIBUNAIS_NO_DATAJUD.has(tribunal)
}

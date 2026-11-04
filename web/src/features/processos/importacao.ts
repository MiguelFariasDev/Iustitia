import { isValidCnj, onlyDigits, formatCnj, getTribunalFromCnj } from '@/lib/validation/documento'
import { parseValor, type ProcessoFormValues } from './schemas'
import { areaDireitoLabels, areasDireito, type AreaDireito } from './types'

/** Colunas do modelo, na ordem em que aparecem na planilha. */
export const colunasModelo = [
  'numero_cnj',
  'documento_cliente',
  'area',
  'tribunal',
  'comarca',
  'vara',
  'assunto',
  'parte_contraria',
  'valor_causa',
  'data_distribuicao',
] as const

export const modeloCsv =
  colunasModelo.join(';') +
  '\n0000832-35.2018.4.01.3202;123.456.789-09;Civel;TRF1;;JEF Adj - Tefé;Concessão;INSS;10000,00;2018-10-29\n'

/**
 * Lê CSV com aspas, BOM e delimitador `;` ou `,` (o Excel em pt-BR exporta com `;`).
 * Devolve linhas de células; linhas totalmente vazias são descartadas.
 */
export function parseCsv(texto: string): string[][] {
  const limpo = texto.replace(/^﻿/, '')
  const primeiraLinha = limpo.split(/\r?\n/, 1)[0] ?? ''
  const delimitador =
    (primeiraLinha.match(/;/g)?.length ?? 0) >= (primeiraLinha.match(/,/g)?.length ?? 0) ? ';' : ','

  const linhas: string[][] = []
  let linha: string[] = []
  let celula = ''
  let entreAspas = false

  for (let i = 0; i < limpo.length; i++) {
    const c = limpo[i]!
    if (entreAspas) {
      if (c === '"' && limpo[i + 1] === '"') {
        celula += '"'
        i++
      } else if (c === '"') {
        entreAspas = false
      } else {
        celula += c
      }
    } else if (c === '"') {
      entreAspas = true
    } else if (c === delimitador) {
      linha.push(celula)
      celula = ''
    } else if (c === '\n' || c === '\r') {
      if (c === '\r' && limpo[i + 1] === '\n') i++
      linha.push(celula)
      celula = ''
      linhas.push(linha)
      linha = []
    } else {
      celula += c
    }
  }
  linha.push(celula)
  linhas.push(linha)

  return linhas.filter((l) => l.some((cell) => cell.trim() !== ''))
}

export interface LinhaImportacao {
  /** Número da linha na planilha (cabeçalho = 1). */
  linha: number
  cnjDigits: string
  documentoCliente: string
  valores: Omit<ProcessoFormValues, 'clienteId'>
  erros: string[]
  /** Repetida dentro do próprio arquivo — só a primeira ocorrência é importada. */
  duplicadaNoArquivo: boolean
}

function semAcento(texto: string) {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim()
}

const areasPorNome = new Map<string, AreaDireito>(
  areasDireito.flatMap((area) => [
    [semAcento(area), area] as const,
    [semAcento(areaDireitoLabels[area]), area] as const,
  ])
)

function parseData(valor: string): string | null {
  const v = valor.trim()
  const br = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(v)
  const iso = br ? `${br[3]}-${br[2]}-${br[1]}` : v
  if (!/^\d{4}-\d{2}-\d{2}$/.test(iso)) return null
  const data = new Date(`${iso}T00:00:00Z`)
  return Number.isNaN(data.getTime()) || data.toISOString().slice(0, 10) !== iso ? null : iso
}

/** Valida a planilha inteira. Cliente e duplicidade no sistema são resolvidos depois (rede). */
export function analisarPlanilha(
  celulas: string[][],
  hoje: Date = new Date()
): { linhas: LinhaImportacao[]; erroCabecalho: string | null } {
  const [cabecalho, ...dados] = celulas
  if (!cabecalho) return { linhas: [], erroCabecalho: 'A planilha está vazia.' }

  const indice = new Map(cabecalho.map((nome, i) => [semAcento(nome).replace(/\s+/g, '_'), i]))
  const faltando = ['numero_cnj', 'documento_cliente'].filter((c) => !indice.has(c))
  if (faltando.length > 0) {
    return { linhas: [], erroCabecalho: `Colunas obrigatórias ausentes: ${faltando.join(', ')}.` }
  }

  const hojeIso = hoje.toISOString().slice(0, 10)
  const vistos = new Set<string>()

  const linhas = dados.map((cells, i): LinhaImportacao => {
    const campo = (nome: (typeof colunasModelo)[number]) => {
      const pos = indice.get(nome)
      return pos === undefined ? '' : (cells[pos] ?? '').trim()
    }
    const erros: string[] = []

    const cnjDigits = onlyDigits(campo('numero_cnj'))
    if (!isValidCnj(cnjDigits)) erros.push('Número CNJ inválido.')

    const documentoCliente = onlyDigits(campo('documento_cliente'))
    if (documentoCliente.length !== 11 && documentoCliente.length !== 14) {
      erros.push('Documento do cliente deve ter 11 (CPF) ou 14 (CNPJ) dígitos.')
    }

    const areaTexto = campo('area')
    const area = areaTexto ? areasPorNome.get(semAcento(areaTexto)) : ('Civel' as AreaDireito)
    if (!area) erros.push(`Área inválida: "${areaTexto}".`)

    const dataTexto = campo('data_distribuicao')
    const data = dataTexto ? parseData(dataTexto) : ''
    if (data === null) erros.push('Data de distribuição inválida (use AAAA-MM-DD ou DD/MM/AAAA).')
    else if (data > hojeIso) erros.push('Data de distribuição no futuro.')

    const valorTexto = campo('valor_causa')
    if (valorTexto && !(parseValor(valorTexto) >= 0)) erros.push('Valor da causa inválido.')

    const tribunalInformado = campo('tribunal').toUpperCase()
    const tribunal = tribunalInformado || getTribunalFromCnj(cnjDigits) || ''

    const duplicadaNoArquivo = cnjDigits !== '' && vistos.has(cnjDigits)
    if (cnjDigits) vistos.add(cnjDigits)

    return {
      linha: i + 2,
      cnjDigits,
      documentoCliente,
      duplicadaNoArquivo,
      erros,
      valores: {
        cnjNumber: formatCnj(cnjDigits),
        area: area ?? 'Civel',
        tribunal,
        comarca: campo('comarca'),
        vara: campo('vara'),
        assunto: campo('assunto'),
        parteContraria: campo('parte_contraria'),
        valorCausa: valorTexto,
        dataDistribuicao: data ?? '',
        observacoes: '',
      },
    }
  })

  return { linhas, erroCabecalho: null }
}

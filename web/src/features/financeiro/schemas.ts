import { z } from 'zod'
import {
  MAX_PARCELAS,
  recorrenciasLancamento,
  statusLancamento,
  tiposLancamento,
  type RecorrenciaLancamento,
  type StatusLancamento,
  type TipoLancamento,
} from './types'

/**
 * Formulário de novo lançamento. `valor` é `number | null`: o CurrencyInput devolve `null`
 * enquanto vazio, e a regra "> 0" mora aqui, sem parsing de texto mascarado.
 */
export const lancamentoFormSchema = z.object({
  tipo: z.enum(tiposLancamento as [TipoLancamento, ...TipoLancamento[]]),
  descricao: z
    .string()
    .trim()
    .min(1, 'Informe a descrição.')
    .max(200, 'A descrição pode ter no máximo 200 caracteres.'),
  /** Cliente (honorário, conta a receber) ou fornecedor (conta a pagar). */
  pessoa: z.string().trim().max(200, 'Use no máximo 200 caracteres.'),
  processo: z.string().trim().max(30, 'Use no máximo 30 caracteres.'),
  valor: z
    .number({ error: 'Informe o valor.' })
    .nullable()
    .refine((v) => v !== null && v > 0, 'O valor deve ser maior que zero.'),
  vencimento: z
    .string()
    .min(1, 'Informe o vencimento.')
    .refine((v) => !Number.isNaN(new Date(`${v}T12:00:00`).getTime()), 'Data inválida.'),
  status: z.enum(statusLancamento as [StatusLancamento, ...StatusLancamento[]]),
  /** À vista, parcelado (valor = total) ou cobrança mensal (valor = de cada mês). */
  recorrencia: z.enum(recorrenciasLancamento as [RecorrenciaLancamento, ...RecorrenciaLancamento[]]),
  parcelas: z.number({ error: 'Informe a quantidade.' }).int('Use um número inteiro.'),
}).superRefine((valores, ctx) => {
  if (valores.recorrencia === 'Unica') return
  if (valores.parcelas < 2 || valores.parcelas > MAX_PARCELAS) {
    ctx.addIssue({
      code: 'custom',
      path: ['parcelas'],
      message: `Informe de 2 a ${MAX_PARCELAS} ${valores.recorrencia === 'Mensal' ? 'meses' : 'parcelas'}.`,
    })
  }
  if (
    valores.recorrencia === 'Parcelado' &&
    valores.valor !== null &&
    valores.parcelas > 0 &&
    Math.floor((valores.valor / valores.parcelas) * 100) < 1
  ) {
    ctx.addIssue({ code: 'custom', path: ['valor'], message: 'Cada parcela precisa ter ao menos R$ 0,01.' })
  }
})

export type LancamentoFormValues = z.infer<typeof lancamentoFormSchema>

export const lancamentoFormDefaults: LancamentoFormValues = {
  tipo: 'Honorario',
  descricao: '',
  pessoa: '',
  processo: '',
  valor: null,
  vencimento: '',
  status: 'Pendente',
  recorrencia: 'Unica',
  parcelas: 12,
}

/**
 * Valores da série como o servidor vai gravar (Lancamento.CriarSerie): no parcelado, os centavos
 * que sobram da divisão vão para a última parcela, para a soma bater com o total.
 */
export function previaSerie(
  recorrencia: RecorrenciaLancamento,
  valor: number | null,
  parcelas: number
): { valorParcela: number; valorUltima: number; total: number } | null {
  if (recorrencia === 'Unica' || valor === null || valor <= 0 || parcelas < 2) return null
  if (recorrencia === 'Mensal') return { valorParcela: valor, valorUltima: valor, total: valor * parcelas }

  const centavos = Math.round(valor * 100)
  const parcela = Math.floor(centavos / parcelas)

  return {
    valorParcela: parcela / 100,
    valorUltima: (centavos - parcela * (parcelas - 1)) / 100,
    total: valor,
  }
}

/** Vencimento da parcela `n` (1 = primeira): mesmo dia nos meses seguintes; dia inexistente vira o último. */
export function vencimentoDaParcela(primeiro: string, n: number): Date | null {
  const [ano, mes, dia] = primeiro.split('-').map(Number)
  if (!ano || !mes || !dia) return null
  const alvo = new Date(ano, mes - 1 + (n - 1), 1)
  const ultimoDia = new Date(alvo.getFullYear(), alvo.getMonth() + 1, 0).getDate()

  return new Date(alvo.getFullYear(), alvo.getMonth(), Math.min(dia, ultimoDia), 12)
}

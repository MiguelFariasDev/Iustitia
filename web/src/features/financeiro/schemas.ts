import { z } from 'zod'
import {
  statusLancamento,
  tiposLancamento,
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
}

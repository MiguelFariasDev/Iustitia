import { z } from 'zod'
import { isValidCnj, onlyDigits } from '@/lib/validation/documento'
import { areasDireito, statusProcesso } from './types'

/**
 * Validação do formulário de processo — espelha Legal.Domain.Processos para avisar antes de
 * enviar. O backend continua sendo a fronteira de confiança.
 *
 * O número CNJ é validado com os dígitos verificadores (mod 97), não só pelo formato: um
 * número com DD errado é dado corrompido e contaminaria a captura de publicações, que é
 * feita exatamente por esse número.
 */
export const processoFormSchema = z.object({
  cnjNumber: z
    .string()
    .trim()
    .min(1, 'Informe o número do processo.')
    .refine((value) => isValidCnj(onlyDigits(value)), 'Número CNJ inválido. Confira os dígitos.'),
  clienteId: z.string().uuid('Selecione o cliente.'),
  area: z.enum(areasDireito as [string, ...string[]]),
  tribunal: z.string().trim().max(20).optional().or(z.literal('')),
  comarca: z.string().trim().max(120).optional().or(z.literal('')),
  vara: z.string().trim().max(160).optional().or(z.literal('')),
  assunto: z.string().trim().max(300).optional().or(z.literal('')),
  parteContraria: z.string().trim().max(200).optional().or(z.literal('')),
  valorCausa: z
    .string()
    .trim()
    .optional()
    .or(z.literal(''))
    .refine(
      (value) => !value || (!Number.isNaN(parseValor(value)) && parseValor(value) >= 0),
      'Valor da causa inválido.'
    ),
  dataDistribuicao: z.string().trim().optional().or(z.literal('')),
  observacoes: z.string().trim().max(2000).optional().or(z.literal('')),
})

export type ProcessoFormValues = z.infer<typeof processoFormSchema>

export const processoFormDefaults: ProcessoFormValues = {
  cnjNumber: '',
  clienteId: '',
  area: 'Civel',
  tribunal: '',
  comarca: '',
  vara: '',
  assunto: '',
  parteContraria: '',
  valorCausa: '',
  dataDistribuicao: '',
  observacoes: '',
}

export const statusProcessoSchema = z.enum(statusProcesso as [string, ...string[]])

/**
 * Converte o valor digitado em pt-BR ("1.234,56") para número. Troca o separador decimal
 * antes de parsear: `Number("1.234,56")` é NaN, e `Number("1.234")` daria 1,234 — um valor
 * de causa mil vezes menor, exatamente o tipo de erro que só aparece num relatório.
 */
export function parseValor(value: string): number {
  return Number(value.replace(/\./g, '').replace(',', '.'))
}

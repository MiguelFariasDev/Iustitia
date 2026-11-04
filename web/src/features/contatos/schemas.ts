import { z } from 'zod'
import { isValidTelefone } from '@/lib/validation/documento'
import { isDocumentoContatoValido } from './documento'
import { tiposContato, type TipoContato } from './types'

export const contatoFormSchema = z.object({
  nome: z
    .string()
    .trim()
    .min(1, 'Informe o nome do contato.')
    .max(200, 'O nome pode ter no máximo 200 caracteres.'),
  tipo: z.enum(tiposContato as [TipoContato, ...TipoContato[]]),
  documento: z
    .string()
    .trim()
    .refine(isDocumentoContatoValido, 'Documento inválido. Informe um CPF ou CNPJ válido.'),
  email: z.string().trim().max(320).email('E-mail inválido.').or(z.literal('')),
  telefone: z
    .string()
    .trim()
    .refine((v) => v === '' || isValidTelefone(v), 'Telefone inválido. Informe DDD e número.'),
  observacoes: z.string().trim().max(2000, 'As observações podem ter no máximo 2000 caracteres.'),
})

export type ContatoFormValues = z.infer<typeof contatoFormSchema>

export const contatoFormDefaults: ContatoFormValues = {
  nome: '',
  tipo: 'ParteContraria',
  documento: '',
  email: '',
  telefone: '',
  observacoes: '',
}

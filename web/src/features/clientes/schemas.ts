import { z } from 'zod'
import { isValidCnpj } from '@/lib/validation/cnpj'
import { isValidCep, isValidCpf, isValidTelefone, onlyDigits } from '@/lib/validation/documento'

/**
 * Validação do formulário de cliente.
 *
 * Espelha as regras do domínio (Legal.Domain.Clientes) para o usuário ser avisado antes de
 * enviar — o backend continua sendo a fronteira de confiança e revalida tudo.
 *
 * O documento é validado POR TIPO com `superRefine`: CPF e CNPJ têm tamanhos e algoritmos
 * diferentes, e um `refine` genérico aceitaria CPF numa empresa.
 */
export const clienteFormSchema = z
  .object({
    tipo: z.enum(['PessoaFisica', 'PessoaJuridica']),
    nome: z
      .string()
      .trim()
      .min(1, 'Informe o nome do cliente.')
      .max(200, 'O nome pode ter no máximo 200 caracteres.'),
    documento: z.string().trim().min(1, 'Informe o CPF ou CNPJ.'),
    email: z.string().trim().email('E-mail inválido.').max(320).optional().or(z.literal('')),
    telefone: z.string().trim().optional().or(z.literal('')),
    dataNascimento: z.string().trim().optional().or(z.literal('')),
    cep: z.string().trim().optional().or(z.literal('')),
    logradouro: z.string().trim().max(200).optional().or(z.literal('')),
    numero: z.string().trim().max(20).optional().or(z.literal('')),
    complemento: z.string().trim().max(100).optional().or(z.literal('')),
    bairro: z.string().trim().max(100).optional().or(z.literal('')),
    cidade: z.string().trim().max(100).optional().or(z.literal('')),
    uf: z.string().trim().max(2).optional().or(z.literal('')),
    observacoes: z.string().trim().max(2000).optional().or(z.literal('')),
  })
  .superRefine((values, ctx) => {
    const documento = onlyDigits(values.documento)

    if (values.tipo === 'PessoaFisica' && !isValidCpf(documento)) {
      ctx.addIssue({ code: 'custom', path: ['documento'], message: 'CPF inválido.' })
    }

    if (values.tipo === 'PessoaJuridica' && !isValidCnpj(documento)) {
      ctx.addIssue({ code: 'custom', path: ['documento'], message: 'CNPJ inválido.' })
    }

    if (values.telefone && !isValidTelefone(values.telefone)) {
      ctx.addIssue({
        code: 'custom',
        path: ['telefone'],
        message: 'Telefone inválido. Informe DDD e número.',
      })
    }

    if (values.cep && !isValidCep(values.cep)) {
      ctx.addIssue({ code: 'custom', path: ['cep'], message: 'CEP inválido.' })
    }

    if (values.dataNascimento) {
      const data = new Date(`${values.dataNascimento}T00:00:00`)

      if (Number.isNaN(data.getTime())) {
        ctx.addIssue({ code: 'custom', path: ['dataNascimento'], message: 'Data inválida.' })
      } else if (data > new Date()) {
        ctx.addIssue({
          code: 'custom',
          path: ['dataNascimento'],
          message: 'A data de nascimento não pode estar no futuro.',
        })
      }

      if (values.tipo === 'PessoaJuridica') {
        ctx.addIssue({
          code: 'custom',
          path: ['dataNascimento'],
          message: 'Data de nascimento só se aplica a pessoa física.',
        })
      }
    }
  })

export type ClienteFormValues = z.infer<typeof clienteFormSchema>

export const clienteFormDefaults: ClienteFormValues = {
  tipo: 'PessoaFisica',
  nome: '',
  documento: '',
  email: '',
  telefone: '',
  dataNascimento: '',
  cep: '',
  logradouro: '',
  numero: '',
  complemento: '',
  bairro: '',
  cidade: '',
  uf: '',
  observacoes: '',
}

import { z } from 'zod'

export const brazilianStates = [
  'AC',
  'AL',
  'AP',
  'AM',
  'BA',
  'CE',
  'DF',
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
  'SP',
  'SE',
  'TO',
] as const

export const emailField = z
  .string()
  .trim()
  .min(1, 'Informe o e-mail.')
  .pipe(z.email('Informe um e-mail válido.'))

/** Política mínima de senha (espelha a configuração do Supabase Auth). */
export const newPasswordField = z
  .string()
  .min(8, 'A senha deve ter pelo menos 8 caracteres.')
  .max(72, 'A senha deve ter no máximo 72 caracteres.')
  .regex(/[a-z]/, 'Inclua ao menos uma letra minúscula.')
  .regex(/[A-Z]/, 'Inclua ao menos uma letra maiúscula.')
  .regex(/\d/, 'Inclua ao menos um número.')

export const personNameField = z
  .string()
  .trim()
  .min(3, 'Informe o nome completo.')
  .max(120, 'Nome muito longo.')

export const phoneField = z
  .string()
  .trim()
  .refine(
    (v) => v === '' || /^\d{10,11}$/.test(v.replace(/\D/g, '')),
    'Telefone inválido (DDD + número).'
  )

export const oabNumberField = z
  .string()
  .trim()
  .refine((v) => v === '' || /^\d{3,7}$/.test(v.replace(/\D/g, '')), 'Número da OAB inválido.')

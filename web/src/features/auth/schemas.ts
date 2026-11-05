import { z } from 'zod'
import { MIN_PASSWORD_LENGTH } from '@/lib/security/passwordStrength'
import { isValidCnpj } from '@/lib/validation/cnpj'
import {
  brazilianStates,
  emailField,
  newPasswordField,
  personNameField,
} from '@/lib/validation/common'

export const loginSchema = z.object({
  email: emailField,
  password: z.string().min(1, 'Informe a senha.'),
})
export type LoginValues = z.infer<typeof loginSchema>

export const forgotPasswordSchema = z.object({ email: emailField })
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>

const passwordsMatch = {
  check: (v: { password: string; confirmPassword: string }) => v.password === v.confirmPassword,
  params: { message: 'As senhas não conferem.', path: ['confirmPassword'] },
}

export const resetPasswordSchema = z
  .object({ password: newPasswordField, confirmPassword: z.string() })
  .refine(passwordsMatch.check, passwordsMatch.params)
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>

export const acceptInviteSchema = z
  .object({
    name: personNameField,
    password: newPasswordField,
    confirmPassword: z.string(),
    acceptTerms: z.boolean().refine((v) => v, 'É necessário aceitar os termos de uso.'),
  })
  .refine(passwordsMatch.check, passwordsMatch.params)
export type AcceptInviteValues = z.infer<typeof acceptInviteSchema>

// ── Cadastro (/cadastro): duas etapas curtas ─────────────────────────────────────────────────

/**
 * Política do cadastro: comprimento acima de tudo (12+), SEM exigir símbolo/maiúscula — frase-senha
 * é bem-vinda (NIST 800-63B). O servidor (Supabase) mantém a política dele; esta é a da tela.
 */
export const signupPasswordField = z
  .string()
  .min(MIN_PASSWORD_LENGTH, `Use pelo menos ${MIN_PASSWORD_LENGTH} caracteres. Uma frase com 3 ou 4 palavras funciona bem.`)
  .max(72, 'A senha deve ter no máximo 72 caracteres.')
  .refine((v) => new Set(v).size >= 5, 'Evite repetir o mesmo caractere.')

export const signupAccountSchema = z
  .object({ name: personNameField, email: emailField, password: signupPasswordField })
  .refine((v) => !v.password.toLowerCase().includes(v.email.split('@')[0]!.toLowerCase()) || v.email.split('@')[0]!.length < 4, {
    message: 'A senha não deve conter o seu e-mail.',
    path: ['password'],
  })
export type SignupAccountValues = z.infer<typeof signupAccountSchema>

export const signupAreas = [
  'Cível',
  'Trabalhista',
  'Tributário',
  'Família',
  'Previdenciário',
  'Outras',
] as const

export const teamSizes = ['1', '2-5', '6-15', '16+'] as const

export const signupOfficeSchema = z.object({
  officeName: z.string().trim().min(3, 'Informe o nome do escritório.').max(120, 'Nome muito longo.'),
  // O backend exige o CNPJ para criar o escritório (Tenant.Create). {{CONFIRMAR}} com o produto
  // como atender advogado autônomo sem CNPJ.
  cnpj: z
    .string()
    .trim()
    .min(1, 'Informe o CNPJ do escritório.')
    .refine((v) => isValidCnpj(v), 'CNPJ inválido.'),
  oabNumber: z
    .string()
    .trim()
    .min(1, 'Informe o número da sua OAB.')
    .refine((v) => /^\d{3,7}$/.test(v.replace(/\D/g, '')), 'Número da OAB inválido.'),
  oabState: z.enum(brazilianStates, { message: 'Selecione a seccional (UF) da OAB.' }),
  teamSize: z.enum(teamSizes, { message: 'Selecione o tamanho da equipe.' }),
  practiceArea: z.enum(signupAreas, { message: 'Selecione a área principal.' }),
  acceptTerms: z
    .boolean()
    .refine((v) => v, 'É necessário aceitar os Termos de Uso e a Política de Privacidade.'),
})
export type SignupOfficeValues = z.infer<typeof signupOfficeSchema>

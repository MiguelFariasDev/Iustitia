import { z } from 'zod'
import { isValidCnpj } from '@/lib/validation/cnpj'
import {
  brazilianStates,
  emailField,
  newPasswordField,
  oabNumberField,
  personNameField,
  phoneField,
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

export const onboardingOfficeSchema = z.object({
  officeName: z.string().trim().min(3, 'Informe o nome do escritório.').max(120),
  cnpj: z
    .string()
    .trim()
    .refine((v) => v === '' || isValidCnpj(v), 'CNPJ inválido.'),
  oabState: z.enum(brazilianStates, { message: 'Selecione a seccional da OAB.' }),
  phone: phoneField,
})

export const onboardingOwnerSchema = z
  .object({
    ownerName: personNameField,
    email: emailField,
    oabNumber: oabNumberField,
    password: newPasswordField,
    confirmPassword: z.string(),
  })
  .refine(passwordsMatch.check, passwordsMatch.params)

export const practiceAreas = [
  'Cível',
  'Trabalhista',
  'Tributário',
  'Família e sucessões',
  'Previdenciário',
  'Empresarial',
  'Criminal',
  'Consumidor',
] as const

export const teamSizes = ['1', '2-5', '6-15', '16+'] as const

export const onboardingPreferencesSchema = z.object({
  teamSize: z.enum(teamSizes, { message: 'Selecione o tamanho da equipe.' }),
  practiceAreas: z.array(z.enum(practiceAreas)).min(1, 'Selecione ao menos uma área de atuação.'),
  acceptTerms: z
    .boolean()
    .refine((v) => v, 'É necessário aceitar os termos de uso e a política de privacidade.'),
  acknowledgeAiReview: z
    .boolean()
    .refine((v) => v, 'É necessário confirmar a ciência sobre a revisão humana obrigatória.'),
})

export type OnboardingOfficeValues = z.infer<typeof onboardingOfficeSchema>
export type OnboardingOwnerValues = z.infer<typeof onboardingOwnerSchema>
export type OnboardingPreferencesValues = z.infer<typeof onboardingPreferencesSchema>

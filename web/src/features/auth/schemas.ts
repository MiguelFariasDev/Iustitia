import { z } from 'zod'
import { isValidCnpj } from '@/lib/validation/cnpj'
import { isValidCpf } from '@/lib/validation/documento'
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

/**
 * Primeira etapa do cadastro. O tipo de conta decide quais campos valem:
 *  - Firm (escritório): nome do escritório e CNPJ obrigatórios (o backend recusa o cadastro sem CNPJ, ADR-068);
 *  - Solo (advogado autônomo): identificado por CPF, que é OBRIGATÓRIO — sem ele a conta não
 *    teria documento nenhum —, e sem nome de escritório (a conta leva o nome do titular).
 *
 * União discriminada, e não um `superRefine` sobre um objeto único, de propósito: o Zod só roda
 * refinamentos de objeto depois que os campos básicos passam, então um erro no CPF só apareceria
 * depois de corrigir a seccional da OAB — duas tentativas para ver todos os erros. Aqui cada
 * tipo valida os próprios campos de uma vez. Os campos do outro tipo são ignorados em vez de
 * recusados: trocar de tipo no meio do preenchimento não obriga a limpar o que foi digitado.
 */
const officeCommon = {
  oabState: z.enum(brazilianStates, { message: 'Selecione a seccional da OAB.' }),
  phone: phoneField,
}

const firmOfficeSchema = z.object({
  accountType: z.literal('Firm'),
  officeName: z.string().trim().min(3, 'Informe o nome do escritório.').max(120),
  cnpj: z
    .string()
    .trim()
    .superRefine((value, ctx) => {
      if (value === '') ctx.addIssue({ code: 'custom', message: 'Informe o CNPJ do escritório.' })
      else if (!isValidCnpj(value)) ctx.addIssue({ code: 'custom', message: 'CNPJ inválido.' })
    }),
  cpf: z.string(),
  ...officeCommon,
})

const soloOfficeSchema = z.object({
  accountType: z.literal('Solo'),
  officeName: z.string(),
  cnpj: z.string(),
  cpf: z
    .string()
    .trim()
    .superRefine((value, ctx) => {
      if (value === '') ctx.addIssue({ code: 'custom', message: 'Informe o seu CPF.' })
      else if (!isValidCpf(value)) ctx.addIssue({ code: 'custom', message: 'CPF inválido.' })
    }),
  ...officeCommon,
})

export const onboardingOfficeSchema = z.discriminatedUnion('accountType', [
  firmOfficeSchema,
  soloOfficeSchema,
])

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

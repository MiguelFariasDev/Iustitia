import { z } from 'zod'
import type { UserRole } from '@/lib/auth/types'
import { isValidCnpj } from '@/lib/validation/cnpj'
import {
  brazilianStates,
  emailField,
  oabNumberField,
  personNameField,
  phoneField,
} from '@/lib/validation/common'

export const profileSchema = z.object({
  name: personNameField,
  email: emailField,
  phone: phoneField,
  oabNumber: oabNumberField,
  oabState: z.union([z.enum(brazilianStates), z.literal('')]),
})
export type ProfileValues = z.infer<typeof profileSchema>

export const tenantProfileSchema = z.object({
  name: z.string().trim().min(3, 'Informe o nome do escritório.').max(120),
  cnpj: z
    .string()
    .trim()
    .refine((v) => v === '' || isValidCnpj(v), 'CNPJ inválido.'),
  oabState: z.enum(brazilianStates, { message: 'Selecione a seccional.' }),
  email: z.union([emailField, z.literal('')]),
  phone: phoneField,
  address: z.string().trim().max(200),
  city: z.string().trim().max(80),
  timezone: z.string().min(1),
})
export type TenantProfileValues = z.infer<typeof tenantProfileSchema>

/** Titular não é atribuível por convite (transferência de titularidade é outro fluxo). */
export const invitableRoles = [
  'Partner',
  'Lawyer',
  'Intern',
  'Admin',
  'Financial',
] as const satisfies readonly UserRole[]

export const inviteUserSchema = z.object({
  email: emailField,
  role: z.enum(invitableRoles, { message: 'Selecione um papel.' }),
})
export type InviteUserValues = z.infer<typeof inviteUserSchema>

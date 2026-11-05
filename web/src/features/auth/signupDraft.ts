import { TERMS_VERSION } from '@/features/landing/LegalPage'

/**
 * Dados do cadastro que o backend ainda não recebe em POST /tenants (OAB do responsável, tamanho
 * da equipe, área principal, aceite dos termos). Ficam neste navegador, SEM a senha, até o
 * primeiro acesso, quando o onboarding os grava nas configurações do escritório. Se a pessoa
 * confirmar o e-mail em outro dispositivo, o rascunho não existe e o onboarding pergunta de novo.
 * {{CONFIRMAR}}: mover para o backend (CreateTenantCommand) elimina essa ponte.
 */
const KEY = 'iu-signup-draft'

export interface SignupDraft {
  email: string
  officeName: string
  oabNumber: string
  oabState: string
  teamSize: string
  practiceArea: string
  /** Aceite dos termos: data e versão (LGPD — consentimento registrado). */
  termsAcceptedAt: string
  termsVersion: string
}

export function saveSignupDraft(draft: Omit<SignupDraft, 'termsAcceptedAt' | 'termsVersion'>) {
  try {
    const value: SignupDraft = { ...draft, termsAcceptedAt: new Date().toISOString(), termsVersion: TERMS_VERSION }
    localStorage.setItem(KEY, JSON.stringify(value))
  } catch {
    // Armazenamento indisponível (modo privado): o onboarding pergunta de novo.
  }
}

export function readSignupDraft(email: string): SignupDraft | null {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return null
    const draft = JSON.parse(raw) as SignupDraft
    return draft.email.toLowerCase() === email.toLowerCase() ? draft : null
  } catch {
    return null
  }
}

export function clearSignupDraft() {
  try {
    localStorage.removeItem(KEY)
  } catch {
    // ignorado
  }
}

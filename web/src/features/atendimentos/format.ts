/**
 * Telefone do WhatsApp (só dígitos, com DDI) em formato legível. Números brasileiros ganham a
 * máscara +55 (85) 99999-8888; qualquer outro é mostrado com "+" na frente, sem inventar máscara.
 */
export function formatPhone(phoneNumber: string): string {
  const digits = phoneNumber.replace(/\D/g, '')

  if (digits.startsWith('55') && (digits.length === 12 || digits.length === 13)) {
    const ddd = digits.slice(2, 4)
    const local = digits.slice(4)
    const split = local.length === 9 ? 5 : 4
    return `+55 (${ddd}) ${local.slice(0, split)}-${local.slice(split)}`
  }

  return digits ? `+${digits}` : phoneNumber
}

/** Nome para exibir: o do contato, ou o telefone quando o WhatsApp não informou nome. */
export function displayName(atendimento: {
  contactName: string | null
  phoneNumber: string
}): string {
  return atendimento.contactName?.trim() || formatPhone(atendimento.phoneNumber)
}

/** Iniciais para o avatar (até duas letras). Sem nome, usa o último dígito do número. */
export function initials(atendimento: { contactName: string | null; phoneNumber: string }): string {
  const name = atendimento.contactName?.trim()
  if (!name) return atendimento.phoneNumber.slice(-2)

  const parts = name.split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
  return (first + last).toUpperCase()
}

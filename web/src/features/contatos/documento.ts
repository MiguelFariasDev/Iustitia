import { formatCnpj, isValidCnpj } from '@/lib/validation/cnpj'
import { formatCpf, isValidCpf, onlyDigits } from '@/lib/validation/documento'

/**
 * Contato não tem "tipo de pessoa": o documento pode ser CPF (testemunha) ou CNPJ
 * (fornecedor). A máscara é decidida pela quantidade de dígitos — até 11 é CPF, depois vira
 * CNPJ — e a mudança acontece sozinha enquanto se digita.
 */
export function formatCpfOuCnpj(value: string): string {
  const digits = onlyDigits(value).slice(0, 14)

  return digits.length <= 11 ? formatCpf(digits) : formatCnpj(digits)
}

/** Vazio é válido (campo opcional); preenchido precisa ser CPF ou CNPJ de verdade. */
export function isDocumentoContatoValido(value: string): boolean {
  const digits = onlyDigits(value)
  if (digits === '') return true

  if (digits.length === 11) return isValidCpf(digits)
  if (digits.length === 14) return isValidCnpj(digits)

  return false
}

/**
 * Medidor de força de senha para o cadastro. Heurística simples e honesta (sem biblioteca
 * pesada): comprimento pesa mais que variedade — frase-senha longa vale mais que `P@ssw0rd!`.
 * Não substitui a política do servidor; só orienta a pessoa enquanto digita.
 */
export type PasswordStrength = { score: 0 | 1 | 2 | 3 | 4; label: string }

const labels = ['Muito fraca', 'Fraca', 'Razoável', 'Boa', 'Forte'] as const

export const MIN_PASSWORD_LENGTH = 12

export function passwordStrength(password: string): PasswordStrength {
  if (!password) return { score: 0, label: labels[0] }

  const unique = new Set(password).size
  // Repetição ("aaaaaaaaaaaa") não conta como comprimento.
  const effectiveLength = Math.min(password.length, unique * 3)
  const words = password.trim().split(/\s+/).filter((w) => w.length >= 3).length
  const classes = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((re) => re.test(password)).length

  let points = 0
  if (effectiveLength >= MIN_PASSWORD_LENGTH) points += 2
  else if (effectiveLength >= 8) points += 1
  if (effectiveLength >= 16) points += 1
  if (classes >= 3) points += 1
  if (words >= 3 && password.length >= 16) points += 1 // frase-senha

  const score = Math.min(4, Math.max(0, points - (effectiveLength < 8 ? 1 : 0))) as 0 | 1 | 2 | 3 | 4
  return { score, label: labels[score] }
}

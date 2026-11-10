/** Saudação pela hora local: "Bom dia" até 11h59, "Boa tarde" até 17h59, depois "Boa noite". */
export function saudacao(now: Date): string {
  const hora = now.getHours()
  if (hora < 12) return 'Bom dia'
  if (hora < 18) return 'Boa tarde'

  return 'Boa noite'
}

const formatadorData = new Intl.DateTimeFormat('pt-BR', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})

/** "Quinta-feira, 5 de novembro" — o dia da semana com inicial maiúscula, sem o ano. */
export function dataPorExtenso(now: Date): string {
  // O Intl devolve "quinta-feira, 5 de novembro".
  const texto = formatadorData.format(now)

  return texto.charAt(0).toUpperCase() + texto.slice(1)
}

const TRATAMENTOS = new Set(['dr', 'dr.', 'dra', 'dra.', 'sr', 'sr.', 'sra', 'sra.'])

/** Primeiro nome para a saudação, ignorando tratamento ("Dra. Ana Souza" → "Ana"). */
export function primeiroNome(nome: string | null | undefined): string {
  const partes = (nome ?? '').trim().split(/\s+/).filter(Boolean)
  const primeira = partes.find((parte) => !TRATAMENTOS.has(parte.toLowerCase()))

  return primeira ?? ''
}

export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}

export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days)
}

export function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  )
}

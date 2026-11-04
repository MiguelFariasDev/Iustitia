/** Datas relativas a "agora" para que os mocks nunca fiquem desatualizados. */
export function daysFromNow(days: number, hour = 9, minute = 0) {
  const d = new Date()
  d.setDate(d.getDate() + days)
  d.setHours(hour, minute, 0, 0)
  return d.toISOString()
}

export function minutesAgo(minutes: number) {
  return new Date(Date.now() - minutes * 60_000).toISOString()
}

const mesesCurtos = [
  'jan',
  'fev',
  'mar',
  'abr',
  'mai',
  'jun',
  'jul',
  'ago',
  'set',
  'out',
  'nov',
  'dez',
]

/** Rótulos curtos dos últimos `count` meses (o atual por último), relativos a `agora`. */
export function ultimosMeses(count: number, agora: Date = new Date()): string[] {
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(agora.getFullYear(), agora.getMonth() - (count - 1 - i), 1)
    return mesesCurtos[d.getMonth()]!
  })
}

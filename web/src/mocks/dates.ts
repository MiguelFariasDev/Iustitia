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

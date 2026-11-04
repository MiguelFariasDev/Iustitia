const locale = 'pt-BR'

const dateFormatter = new Intl.DateTimeFormat(locale, { dateStyle: 'short' })
const dateTimeFormatter = new Intl.DateTimeFormat(locale, {
  dateStyle: 'short',
  timeStyle: 'short',
})
const timeFormatter = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' })
const numberFormatter = new Intl.NumberFormat(locale)
const relativeFormatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
const currencyFormatter = new Intl.NumberFormat(locale, { style: 'currency', currency: 'BRL' })

export const formatDate = (value: string | Date) => dateFormatter.format(new Date(value))
export const formatDateTime = (value: string | Date) => dateTimeFormatter.format(new Date(value))
export const formatTime = (value: string | Date) => timeFormatter.format(new Date(value))
export const formatNumber = (value: number) => numberFormatter.format(value)

/** Valor monetário em reais (valor da causa, honorários). */
export const formatCurrency = (value: number) => currencyFormatter.format(value)

const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

export function formatRelative(value: string | Date, now: Date = new Date()) {
  const diff = new Date(value).getTime() - now.getTime()
  const abs = Math.abs(diff)
  if (abs < MINUTE) return 'agora'
  if (abs < HOUR) return relativeFormatter.format(Math.round(diff / MINUTE), 'minute')
  if (abs < DAY) return relativeFormatter.format(Math.round(diff / HOUR), 'hour')
  return relativeFormatter.format(Math.round(diff / DAY), 'day')
}

export function getInitials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
  return (first + last).toUpperCase() || '?'
}

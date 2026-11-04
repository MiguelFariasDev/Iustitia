/** Normaliza para busca sem acento/caixa ("Ação" casa com "acao"). */
export function normalizeSearch(value: string) {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
    .trim()
}

export function matchesSearch(haystack: string, term: string) {
  const needle = normalizeSearch(term)
  return needle.length === 0 || normalizeSearch(haystack).includes(needle)
}

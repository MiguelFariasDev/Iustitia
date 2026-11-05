import { normalizeFilters, processosQuery } from './queries'

describe('chave de cache de processos', () => {
  it('muda quando muda qualquer filtro enviado ao servidor', () => {
    const base = processosQuery({}).queryKey

    for (const filtro of [
      { responsavelUserId: 'u-2' },
      { tribunal: 'TJCE' },
      { distribuicaoDe: '2026-01-01' },
      { distribuicaoAte: '2026-12-31' },
      { area: 'Civel' as const },
      { status: 'Ativo' as const },
    ]) {
      expect(processosQuery(filtro).queryKey).not.toEqual(base)
    }
  })

  it('trata ausente, null e vazio como a mesma consulta', () => {
    expect(normalizeFilters({ tribunal: '  ', responsavelUserId: null })).toEqual(
      normalizeFilters({})
    )
  })
})

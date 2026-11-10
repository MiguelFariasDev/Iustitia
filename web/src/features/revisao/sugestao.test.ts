import type { Publication } from '@/features/publicacoes/types'
import { sugerirDemo } from './sugestao'

const pub = (extra: Partial<Publication>): Pick<Publication, 'id' | 'kind' | 'content'> => ({
  id: 'pub-1',
  kind: 'citation',
  content: 'Texto sem prazo.',
  ...extra,
})

describe('sugerirDemo', () => {
  it('lê o prazo escrito no texto e marca o trecho que o gerou', () => {
    const content =
      'Intime-se a ré para contestar no prazo de 15 (quinze) dias úteis, sob pena de revelia.'
    const s = sugerirDemo(pub({ content }))

    expect(s.dias).toBe(15)
    expect(content.slice(s.trecho!.inicio, s.trecho!.fim)).toBe('15 (quinze) dias úteis')
    expect(s.fundamento).toMatch(/fixado no próprio texto/)
    // Não cita artigo da tabela por tipo, que poderia contradizer o prazo lido do texto.
    expect(s.fundamento).not.toMatch(/art\./)
    expect(s.confianca).toBeGreaterThanOrEqual(88)
  })

  it('aceita "dias" sem "úteis"', () => {
    const s = sugerirDemo(pub({ content: 'Manifeste-se no prazo de 5 (cinco) dias.' }))

    expect(s.dias).toBe(5)
    expect(s.trecho).not.toBeNull()
  })

  it('sem prazo no texto, usa a tabela do tipo e não marca trecho nem finge certeza', () => {
    const s = sugerirDemo(
      pub({ kind: 'sentence', content: 'Sentença: julgo procedente o pedido.' })
    )

    expect(s).toMatchObject({ tipo: 'Recurso de apelação', dias: 15, trecho: null })
    expect(s.confianca).toBeLessThan(88)
  })

  it('o prazo do texto vence a tabela por tipo', () => {
    const s = sugerirDemo(pub({ kind: 'citation', content: 'Responda em 10 (dez) dias úteis.' }))

    expect(s.dias).toBe(10)
  })

  it('é determinística: a mesma publicação sempre recebe a mesma sugestão', () => {
    expect(sugerirDemo(pub({}))).toEqual(sugerirDemo(pub({})))
  })

  it('a confiança fica sempre entre 0 e 100', () => {
    for (let i = 0; i < 50; i++) {
      const { confianca } = sugerirDemo(pub({ id: `pub-${i}` }))
      expect(confianca).toBeGreaterThan(0)
      expect(confianca).toBeLessThanOrEqual(100)
    }
  })
})

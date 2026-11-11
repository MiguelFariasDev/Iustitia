import { alternarTag, criarTag, lerEstadoLocal, removerTag, tagsDe } from './repositorio'

describe('tags (armazém local do modo de mocks)', () => {
  it('cria, aplica no processo e no advogado, e reaproveita nome repetido', () => {
    const urgente = criarTag('  Urgente ')!
    alternarTag('processo', 'p1', urgente.id)
    alternarTag('advogado', 'u1', urgente.id)

    const estado = lerEstadoLocal()
    expect(criarTag('urgente')?.id).toBe(urgente.id)
    expect(tagsDe(estado, 'processo', 'p1').map((t) => t.nome)).toEqual(['Urgente'])
    expect(tagsDe(estado, 'advogado', 'u1')).toHaveLength(1)
    expect(tagsDe(estado, 'processo', 'p2')).toHaveLength(0)
  })

  it('alternar de novo tira a tag; apagar remove de todos os lugares', () => {
    const tag = criarTag('Prioridade')!
    alternarTag('processo', 'p1', tag.id)
    alternarTag('processo', 'p1', tag.id)
    expect(tagsDe(lerEstadoLocal(), 'processo', 'p1')).toHaveLength(0)

    alternarTag('processo', 'p1', tag.id)
    removerTag(tag.id)
    expect(lerEstadoLocal().tags).toHaveLength(0)
    expect(tagsDe(lerEstadoLocal(), 'processo', 'p1')).toHaveLength(0)
  })

  it('ignora nome vazio', () => {
    expect(criarTag('   ')).toBeNull()
  })
})

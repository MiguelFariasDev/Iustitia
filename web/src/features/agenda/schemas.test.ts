import { eventoFormDefaults, eventoFormSchema, formToPayload } from './schemas'

const valid = { ...eventoFormDefaults('2026-11-10T14:00'), titulo: 'Audiência' }

describe('eventoFormSchema', () => {
  it('aceita o mínimo: título e início', () => {
    expect(eventoFormSchema.safeParse(valid).success).toBe(true)
  })

  it('exige título', () => {
    const result = eventoFormSchema.safeParse({ ...valid, titulo: '   ' })

    expect(result.success).toBe(false)
  })

  it('recusa término anterior ao início, apontando o campo "fim"', () => {
    const result = eventoFormSchema.safeParse({ ...valid, fim: '2026-11-10T13:00' })

    expect(result.success).toBe(false)
    if (!result.success) expect(result.error.issues[0]?.path).toEqual(['fim'])
  })

  it('aceita término igual ao início', () => {
    expect(eventoFormSchema.safeParse({ ...valid, fim: '2026-11-10T14:00' }).success).toBe(true)
  })
})

describe('formToPayload', () => {
  it('converte o horário local para ISO e deixa o término vazio como null', () => {
    const payload = formToPayload(valid)

    expect(payload.inicio).toBe(new Date('2026-11-10T14:00').toISOString())
    expect(payload.fim).toBeNull()
    expect(payload.processoId).toBeNull()
  })

  it('dia inteiro vai à meia-noite local e sem término', () => {
    const payload = formToPayload({ ...valid, diaInteiro: true, fim: '2026-11-12T10:00' })

    expect(payload.inicio).toBe(new Date('2026-11-10T00:00').toISOString())
    expect(payload.fim).toBeNull()
  })

  it('mantém o processo vinculado', () => {
    expect(formToPayload({ ...valid, processoId: 'p-1' }).processoId).toBe('p-1')
  })
})

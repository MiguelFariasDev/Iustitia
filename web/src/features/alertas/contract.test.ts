import { parseRealtimePayload, toAlerta, toAlertasSummary } from './contract'

describe('contrato de alertas', () => {
  it('traduz o alerta da API', () => {
    expect(
      toAlerta({
        id: 'a1',
        type: 'Publication',
        title: 'Nova publicação',
        description: 'Processo X',
        referenceId: 'pub-1',
        isRead: false,
        createdAt: '2026-10-07T10:00:00Z',
        readAt: null,
      })
    ).toMatchObject({ id: 'a1', type: 'Publication', isRead: false, referenceId: 'pub-1' })
  })

  it('trata um tipo desconhecido como "Other" — tipo novo do backend não quebra a tela', () => {
    expect(
      toAlerta({
        id: 'a1',
        type: 'TipoDoFuturo',
        title: 't',
        description: null,
        referenceId: null,
        isRead: true,
        createdAt: '2026-10-07T10:00:00Z',
        readAt: null,
      }).type
    ).toBe('Other')
  })

  it('soma tipos desconhecidos em "Other" nas contagens', () => {
    const summary = toAlertasSummary({
      total: 6,
      unread: 3,
      unreadByType: { Publication: 2, Futuro: 1 },
      totalByType: { Publication: 4, Task: 1, Futuro: 1 },
    })

    expect(summary.unreadByType).toEqual({ Publication: 2, Other: 1 })
    expect(summary.totalByType).toEqual({ Publication: 4, Task: 1, Other: 1 })
  })
})

describe('payload do canal Realtime', () => {
  it('aceita o payload completo', () => {
    expect(
      parseRealtimePayload({
        alertId: 'a1',
        type: 'Publication',
        title: 'Nova publicação',
        description: 'Processo X',
        referenceId: 'pub-1',
        createdAt: '2026-10-07T10:00:00Z',
      })
    ).toEqual({
      alertId: 'a1',
      type: 'Publication',
      title: 'Nova publicação',
      description: 'Processo X',
      referenceId: 'pub-1',
      createdAt: '2026-10-07T10:00:00Z',
    })
  })

  it.each([null, undefined, 'texto', 42, [], {}, { alertId: 1, title: 'x' }, { alertId: 'a' }])(
    'rejeita payload malformado: %j',
    (value) => {
      // Dado de rede nunca é confiável só por vir do nosso backend.
      expect(parseRealtimePayload(value)).toBeNull()
    }
  )

  it('preenche o que for opcional', () => {
    const parsed = parseRealtimePayload({ alertId: 'a1', title: 't' })

    expect(parsed).toMatchObject({ type: 'Other', description: null, referenceId: null })
    expect(parsed?.createdAt).toBeTruthy()
  })
})

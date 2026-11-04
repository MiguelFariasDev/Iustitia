import type { AxiosInstance } from 'axios'
import { getDashboardSummary } from './api'

// A composição contra a API .NET é o que se verifica; o ramo simulado é desligado.
vi.mock('@/config/env', () => ({ env: { useMocks: false }, isMockAuth: false }))

/** Cliente Axios mínimo: só o `get`, que é tudo que a composição do painel usa. */
function fakeClient(routes: Record<string, unknown>): AxiosInstance {
  const get = vi.fn((url: string, _config?: unknown) => {
    if (!(url in routes)) throw new Error(`rota não esperada: ${url}`)
    return Promise.resolve({ data: routes[url] })
  })

  return { get } as unknown as AxiosInstance
}

const summary = {
  total: 1801,
  pendingReview: 1801,
  unread: 1795,
  pendingToday: 3,
  treatedThisWeek: 9,
}
const processos = { items: [], page: 1, pageSize: 1, totalCount: 12, totalPages: 12 }
const alertas = {
  items: [
    {
      id: 'alert-1',
      type: 'Publication',
      title: 'Nova publicação',
      description: 'Processo 0000777-07.2018.8.06.0100',
      referenceId: 'pub-1',
      isRead: false,
      createdAt: '2026-09-28T12:00:00+00:00',
      readAt: null,
    },
  ],
  page: 1,
  pageSize: 5,
  totalCount: 4,
  totalPages: 1,
}
const publications = {
  items: [
    {
      id: 'pub-1',
      processId: null,
      clienteId: 'cli-1',
      clienteNome: 'Maria Silva',
      responsavelUserId: null,
      tribunal: 'TJCE',
      cnjNumber: '0000777-07.2018.8.06.0100',
      source: 'DJEN',
      contentPreview: 'Fica a parte intimada…',
      publishedAt: '2026-09-27T00:00:00+00:00',
      capturedAt: '2026-09-28T12:00:00+00:00',
      type: null,
      status: 'PendingReview',
      isRead: false,
    },
  ],
  page: 1,
  pageSize: 5,
  totalCount: 1801,
  totalPages: 361,
}

function routes(overrides: Record<string, unknown> = {}) {
  return {
    '/publications/summary': summary,
    '/publications': publications,
    '/processos': processos,
    '/alerts': alertas,
    ...overrides,
  }
}

describe('composição do painel a partir dos endpoints de cada módulo', () => {
  it('usa números REAIS para publicações e alertas', async () => {
    const result = await getDashboardSummary(fakeClient(routes()))

    expect(result.metrics.unreadPublications.value).toBe(1795)
    expect(result.metrics.pendingReviews.value).toBe(1801)
    expect(result.metrics.unreadAlerts.value).toBe(4)
    expect(result.publications).toHaveLength(1)
    expect(result.publications[0]?.processNumber).toBe('0000777-07.2018.8.06.0100')
    expect(result.alerts[0]?.title).toBe('Nova publicação')
  })

  it('mostra o NOME DO CLIENTE e o tribunal, vindos do vínculo da captura', async () => {
    const result = await getDashboardSummary(fakeClient(routes()))

    expect(result.publications[0]?.client).toBe('Maria Silva')
    expect(result.publications[0]?.source).toBe('TJCE')
  })

  it('pede só as NÃO LIDAS, limitadas, tanto de publicações quanto de alertas', async () => {
    const client = fakeClient(routes())

    await getDashboardSummary(client)

    expect(vi.mocked(client.get)).toHaveBeenCalledWith('/publications', {
      params: { page: 1, pageSize: 5, isRead: false },
    })
    expect(vi.mocked(client.get)).toHaveBeenCalledWith('/alerts', {
      params: { page: 1, pageSize: 5, isRead: false },
    })
  })

  it('NÃO chama endpoints inexistentes (/dashboard/summary, /me/pending-counts)', async () => {
    const client = fakeClient(routes())

    await getDashboardSummary(client)

    const calledUrls = vi.mocked(client.get).mock.calls.map(([url]) => url)
    expect(calledUrls).not.toContain('/dashboard/summary')
    expect(calledUrls).not.toContain('/me/pending-counts')
  })

  it('conta processos ATIVOS a partir do endpoint real', async () => {
    const client = fakeClient(routes())

    const result = await getDashboardSummary(client)

    expect(result.metrics.activeProcesses.value).toBe(12)
    // pageSize 1: a consulta existe pelo totalCount, não para trazer linhas.
    expect(vi.mocked(client.get)).toHaveBeenCalledWith('/processos', {
      params: { page: 1, pageSize: 1, status: 'Ativo' },
    })
  })

  it('não expõe agenda nem prazos — não existem no backend', async () => {
    // Um número inventado num painel vira decisão errada.
    const result = await getDashboardSummary(fakeClient(routes()))

    expect(result).not.toHaveProperty('agenda')
    expect(result.metrics).not.toHaveProperty('deadlinesThisWeek')
  })

  it('exibe estado vazio quando o escritório ainda não tem publicações nem alertas', async () => {
    const empty = { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }
    const result = await getDashboardSummary(
      fakeClient(
        routes({
          '/publications/summary': { ...summary, total: 0, pendingReview: 0, unread: 0 },
          '/publications': empty,
          '/alerts': empty,
          '/processos': { ...empty, pageSize: 1 },
        })
      )
    )

    expect(result.metrics.unreadPublications.value).toBe(0)
    expect(result.publications).toEqual([])
    expect(result.alerts).toEqual([])
  })

  it('rotula publicação sem processo vinculado em vez de deixar vazio', async () => {
    const result = await getDashboardSummary(
      fakeClient(
        routes({
          '/publications': {
            ...publications,
            items: [{ ...publications.items[0], cnjNumber: '' }],
          },
        })
      )
    )

    expect(result.publications[0]?.processNumber).toBe('Sem processo vinculado')
  })
})

import type { AxiosInstance } from 'axios'

vi.mock('@/config/env', () => ({ env: { useMocks: false }, isMockAuth: false }))

const patch = vi.fn()
const del = vi.fn()
vi.mock('@/lib/api/client', () => ({
  apiClient: {
    patch: (...args: unknown[]) => patch(...args),
    delete: (...args: unknown[]) => del(...args),
  },
}))

import {
  dismissAlerta,
  getAlertas,
  getAlertasSummary,
  markAlertaAsRead,
  markAllAlertasAsRead,
} from './api'

function fakeClient(data: unknown) {
  const get = vi.fn(() => Promise.resolve({ data }))
  return { client: { get } as unknown as AxiosInstance, get }
}

describe('API de alertas', () => {
  beforeEach(() => {
    patch.mockReset()
    del.mockReset()
  })

  it('envia vários tipos como lista separada por vírgula', async () => {
    const { client, get } = fakeClient({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    })

    await getAlertas(
      { types: ['Task', 'Birthday', 'Other'], isRead: false, from: '2026-10-01' },
      client
    )

    expect(get).toHaveBeenCalledWith('/alerts', {
      params: {
        page: 1,
        pageSize: 20,
        type: 'Task,Birthday,Other',
        isRead: false,
        from: '2026-10-01',
        to: undefined,
      },
    })
  })

  it('sem tipo, não envia o parâmetro', async () => {
    const { client, get } = fakeClient({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    })

    await getAlertas({ types: [] }, client)

    expect(
      (get.mock.calls[0] as unknown as [string, { params: { type?: string } }])[1].params.type
    ).toBeUndefined()
  })

  it('lê o resumo com as contagens por tipo', async () => {
    const { client } = fakeClient({
      total: 5,
      unread: 2,
      unreadByType: { Publication: 2 },
      totalByType: { Publication: 4, Task: 1 },
    })

    expect(await getAlertasSummary(client)).toEqual({
      total: 5,
      unread: 2,
      unreadByType: { Publication: 2 },
      totalByType: { Publication: 4, Task: 1 },
    })
  })

  it('usa PATCH para ler, PATCH para ler tudo e DELETE para descartar', async () => {
    patch.mockResolvedValueOnce({ data: undefined }).mockResolvedValueOnce({ data: 3 })

    await markAlertaAsRead('a1')
    expect(await markAllAlertasAsRead()).toBe(3)
    await dismissAlerta('a1')

    expect(patch).toHaveBeenNthCalledWith(1, '/alerts/a1/read')
    expect(patch).toHaveBeenNthCalledWith(2, '/alerts/read-all')
    expect(del).toHaveBeenCalledWith('/alerts/a1')
  })
})

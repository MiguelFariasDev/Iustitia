import type { AxiosInstance } from 'axios'

// Estes testes verificam a conversa com a API .NET, então o ramo de mock é desligado.
vi.mock('@/config/env', () => ({ env: { useMocks: false }, isMockAuth: false }))

const patch = vi.fn()
vi.mock('@/lib/api/client', () => ({
  apiClient: { patch: (...args: unknown[]) => patch(...args) },
}))

import {
  changePublicationStatus,
  getPublicationById,
  getPublications,
  getPublicationsSummary,
  markPublicationAsRead,
} from './api'

function fakeClient(data: unknown) {
  const get = vi.fn(() => Promise.resolve({ data }))
  return { client: { get } as unknown as AxiosInstance, get }
}

const page = { items: [], page: 2, pageSize: 20, totalCount: 41, totalPages: 3 }

describe('API de publicações', () => {
  beforeEach(() => patch.mockReset())

  it('traduz os filtros da tela para os parâmetros da API', async () => {
    const { client, get } = fakeClient(page)

    await getPublications(
      {
        page: 2,
        status: 'treated',
        tribunal: 'TJSP',
        clienteId: 'c1',
        processId: 'p1',
        from: '2026-09-01',
        to: '2026-09-30',
        isRead: false,
        busca: 'silva',
        ordem: 'tribunal',
      },
      client
    )

    expect(get).toHaveBeenCalledWith('/publications', {
      params: {
        page: 2,
        pageSize: 20,
        // "Tratada" na tela são dois status do domínio.
        status: 'Approved,Corrected',
        tribunal: 'TJSP',
        clienteId: 'c1',
        processId: 'p1',
        dataInicio: '2026-09-01',
        dataFim: '2026-09-30',
        isRead: false,
        busca: 'silva',
        ordem: 'tribunal',
      },
    })
  })

  it('não envia parâmetros vazios nem a ordenação padrão', async () => {
    const { client, get } = fakeClient(page)

    await getPublications({ busca: '', tribunal: null, ordem: 'recent' }, client)

    const params = (
      get.mock.calls[0] as unknown as [string, { params: Record<string, unknown> }]
    )[1].params
    expect(params).toEqual({
      page: 1,
      pageSize: 20,
      status: undefined,
      tribunal: undefined,
      clienteId: undefined,
      processId: undefined,
      dataInicio: undefined,
      dataFim: undefined,
      isRead: undefined,
      busca: undefined,
      ordem: undefined,
    })
  })

  it('devolve a paginação da API e as publicações traduzidas', async () => {
    const { client } = fakeClient({
      ...page,
      items: [
        {
          id: 'p1',
          processId: 'proc',
          clienteId: 'c1',
          clienteNome: 'Maria',
          responsavelUserId: null,
          tribunal: 'TJSP',
          cnjNumber: '1234567-48.2026.8.26.0001',
          source: 'DJEN',
          contentPreview: 'trecho',
          publishedAt: '2026-09-27T00:00:00Z',
          capturedAt: '2026-09-28T00:00:00Z',
          type: 'Intimacao',
          status: 'Approved',
          isRead: true,
        },
      ],
    })

    const result = await getPublications({}, client)

    expect(result).toMatchObject({ page: 2, totalCount: 41, totalPages: 3 })
    expect(result.items[0]).toMatchObject({
      kind: 'intimation',
      status: 'treated',
      clienteNome: 'Maria',
    })
  })

  it('lê o resumo (hoje, semana, pendentes, não lidas)', async () => {
    const { client } = fakeClient({
      total: 9,
      pendingReview: 4,
      unread: 3,
      pendingToday: 2,
      treatedThisWeek: 5,
    })

    expect(await getPublicationsSummary(client)).toEqual({
      total: 9,
      untreatedTotal: 4,
      unread: 3,
      untreatedToday: 2,
      treatedThisWeek: 5,
    })
  })

  it('o detalhe traz o inteiro teor e o link do DJEN (só https)', async () => {
    const { client } = fakeClient({
      id: 'p1',
      tenantId: 't',
      externalId: 'e',
      processId: null,
      clienteId: null,
      clienteNome: null,
      responsavelUserId: null,
      tribunal: null,
      cnjNumber: '1',
      source: 'DJEN',
      rawContent: 'texto integral',
      publishedAt: '2026-09-27T00:00:00Z',
      capturedAt: '2026-09-28T00:00:00Z',
      type: null,
      status: 'PendingReview',
      isRead: false,
      metadataJson: '{"link":"https://djen.example/x","orgao":"1ª Vara"}',
      reviewNotes: null,
    })

    const publication = await getPublicationById('p1', client)

    expect(publication.content).toBe('texto integral')
    expect(publication.metadata?.link).toBe('https://djen.example/x')
  })

  it('marca como lida e muda o status por PATCH (não POST)', async () => {
    await markPublicationAsRead('p1')
    await changePublicationStatus('p1', 'Approved', 'conferido')

    expect(patch).toHaveBeenNthCalledWith(1, '/publications/p1/read')
    expect(patch).toHaveBeenNthCalledWith(2, '/publications/p1/status', {
      status: 'Approved',
      notes: 'conferido',
    })
  })
})

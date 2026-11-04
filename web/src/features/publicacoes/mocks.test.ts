import {
  mockChangeStatus,
  mockGetPublication,
  mockListPublications,
  mockMarkAsRead,
  mockPublicationsSummary,
  resetPublicationsMock,
} from './mocks'

beforeEach(() => resetPublicationsMock())

describe('backend simulado de publicações (usado em dev e nos E2E)', () => {
  it('pagina e informa o total, como a API', () => {
    const first = mockListPublications({ page: 1, pageSize: 10 })

    expect(first.items).toHaveLength(10)
    expect(first.totalCount).toBe(27)
    expect(first.totalPages).toBe(3)
    expect(mockListPublications({ page: 3, pageSize: 10 }).items).toHaveLength(7)
  })

  it('ordena da mais recente para a mais antiga por padrão, e inverte em "oldest"', () => {
    const recent = mockListPublications({ pageSize: 50 }).items.map((i) => i.publishedAt)
    const oldest = mockListPublications({ pageSize: 50, ordem: 'oldest' }).items.map(
      (i) => i.publishedAt
    )

    expect(recent).toEqual([...recent].sort().reverse())
    expect(oldest).toEqual([...oldest].sort())
  })

  it('filtra por tribunal, cliente, leitura e número do processo', () => {
    const tjsp = mockListPublications({ tribunal: 'TJSP', pageSize: 50 })
    expect(tjsp.items.length).toBeGreaterThan(0)
    expect(tjsp.items.every((i) => i.tribunal === 'TJSP')).toBe(true)

    const unread = mockListPublications({ isRead: false, pageSize: 50 })
    expect(unread.items.every((i) => !i.isRead)).toBe(true)

    const porCliente = mockListPublications({ busca: 'horizonte', pageSize: 50 })
    expect(porCliente.items.every((i) => i.clienteNome?.includes('Horizonte'))).toBe(true)

    const numero = mockListPublications({ busca: '5001003', pageSize: 50 })
    expect(numero.items).toHaveLength(1)
  })

  describe('transições de status (mesmas regras de Publication.ChangeStatus)', () => {
    it('pendente vira tratada e o resumo reflete', () => {
      const before = mockPublicationsSummary()

      expect(mockChangeStatus('pub-001', 'Approved', 'ok')).toBe('ok')

      expect(mockGetPublication('pub-001')).toMatchObject({ status: 'treated', reviewNotes: 'ok' })
      expect(mockPublicationsSummary().untreatedTotal).toBe(before.untreatedTotal - 1)
      expect(mockPublicationsSummary().treatedThisWeek).toBe(before.treatedThisWeek + 1)
    })

    it('uma publicação revisada não volta a pendente nem muda de desfecho', () => {
      mockChangeStatus('pub-001', 'Approved')

      expect(mockChangeStatus('pub-001', 'Rejected')).toBe('invalid-transition')
      expect(mockChangeStatus('pub-001', 'Approved')).toBe('invalid-transition')
    })

    it('qualquer desfecho pode ser arquivado, e arquivada é terminal', () => {
      mockChangeStatus('pub-001', 'Approved')

      expect(mockChangeStatus('pub-001', 'Archived')).toBe('ok')
      expect(mockChangeStatus('pub-001', 'Archived')).toBe('invalid-transition')
    })

    it('id inexistente é "não encontrada"', () => {
      expect(mockChangeStatus('nao-existe', 'Approved')).toBe('not-found')
    })
  })

  it('marcar como lida duas vezes é conflito, e não altera o status de revisão', () => {
    const id = mockListPublications({ isRead: false }).items[0]!.id

    expect(mockMarkAsRead(id)).toBe('ok')
    expect(mockMarkAsRead(id)).toBe('already-read')
    expect(mockGetPublication(id)?.status).toBe('untreated')
  })
})

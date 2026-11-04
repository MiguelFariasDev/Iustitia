import type { AxiosInstance } from 'axios'
import { getPendingCounts } from './api'

// Os resumos têm um ramo de mock quando NEXT_PUBLIC_API_MOCKS=true (o padrão nos testes).
// Aqui o que se verifica é a chamada ao backend de cada módulo, então o mock é desligado.
vi.mock('@/config/env', () => ({ env: { useMocks: false }, isMockAuth: false }))

const publications = {
  total: 100,
  pendingReview: 42,
  unread: 7,
  pendingToday: 3,
  treatedThisWeek: 5,
}
const alerts = { total: 9, unread: 4, unreadByType: {}, totalByType: {} }

function fakeClient(): AxiosInstance {
  return {
    get: vi.fn((url: string) =>
      Promise.resolve({ data: url === '/alerts/summary' ? alerts : publications })
    ),
  } as unknown as AxiosInstance
}

describe('contadores do badge de navegação', () => {
  it('conta o que EXIGE AÇÃO (pendente de revisão), não o que está por ler', async () => {
    // Uma publicação já lida mas ainda não revisada continua sendo um prazo em aberto.
    const counts = await getPendingCounts(fakeClient())

    expect(counts.publications).toBe(42)
  })

  it('conta os alertas NÃO LIDOS', async () => {
    expect((await getPendingCounts(fakeClient())).alerts).toBe(4)
  })

  it('usa os endpoints de cada módulo, não um agregador inexistente', async () => {
    const client = fakeClient()

    await getPendingCounts(client)

    expect(client.get).toHaveBeenCalledWith('/publications/summary')
    expect(client.get).toHaveBeenCalledWith('/alerts/summary')
  })
})

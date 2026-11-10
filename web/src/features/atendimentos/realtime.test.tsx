import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { createTestQueryClient } from '@/test/render'
import { useAuthStore } from '@/stores/authStore'
import { queryKeys } from '@/lib/query/queryKeys'
import { resetMockAtendimentos } from './mocks'
import {
  atendimentosChannelName,
  MOCK_ATENDIMENTO_EVENT,
  useAtendimentosRealtime,
} from './realtime'
import { notify } from '@/lib/notify'

vi.mock('@/lib/notify', () => ({
  notify: { info: vi.fn(), success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}))

function signIn(
  role: 'Lawyer' | 'Intern' | 'Owner' | 'Partner' | 'Admin',
  tenantId: string | null = 'tenant-1'
) {
  useAuthStore.getState().setSession({
    accessToken: 't',
    expiresAt: null,
    user: { id: 'adv-1', email: 'a@b.c', name: 'Ana', avatarUrl: null, role, tenantId },
  })
}

function mount() {
  const queryClient = createTestQueryClient()
  const spy = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  renderHook(() => useAtendimentosRealtime(), { wrapper })
  return spy
}

const clientWrites = (detail: Record<string, unknown>) =>
  act(() => {
    window.dispatchEvent(new CustomEvent(MOCK_ATENDIMENTO_EVENT, { detail }))
  })

beforeEach(() => resetMockAtendimentos())
afterEach(() => useAuthStore.getState().clear())

describe('useAtendimentosRealtime (modo simulado)', () => {
  it('o canal é o do ESCRITÓRIO, não o do usuário', () => {
    expect(atendimentosChannelName('t-9')).toBe('tenant:t-9:atendimentos')
  })

  it('uma mensagem do cliente invalida a fila e a conversa', async () => {
    signIn('Lawyer')
    const invalidate = mount()

    clientWrites({ atendimentoId: 'atd-004', text: 'oi' })

    await waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.atendimentos.all })
    )
  })

  it('o cliente digitar "2" avisa o advogado de que alguém aguarda atendimento', async () => {
    signIn('Lawyer')
    mount()

    clientWrites({ phoneNumber: '5585966665555', text: '2' })

    await waitFor(() =>
      expect(notify.info).toHaveBeenCalledWith('Um cliente está aguardando atendimento.')
    )
  })

  it('mensagem comum NÃO gera toast (só o pedido de atendimento exige ação)', async () => {
    signIn('Lawyer')
    const invalidate = mount()

    clientWrites({ atendimentoId: 'atd-004', text: 'bom dia' })

    await waitFor(() => expect(invalidate).toHaveBeenCalled())
    expect(notify.info).not.toHaveBeenCalled()
  })

  it('evento para uma conversa que não existe é ignorado, sem lançar', () => {
    signIn('Lawyer')
    mount()

    expect(() => clientWrites({ atendimentoId: 'fantasma', text: 'oi' })).not.toThrow()
    expect(notify.info).not.toHaveBeenCalled()
  })

  it.each(['Intern', 'Admin'] as const)(
    '%s não pode ler conversas: nem escuta o canal',
    async (role) => {
      signIn(role)
      const invalidate = mount()

      clientWrites({ atendimentoId: 'atd-004', text: '2' })

      expect(invalidate).not.toHaveBeenCalled()
      expect(notify.info).not.toHaveBeenCalled()
    }
  )

  it.each(['Owner', 'Partner', 'Lawyer'] as const)('%s escuta o canal', async (role) => {
    signIn(role)
    const invalidate = mount()

    clientWrites({ atendimentoId: 'atd-004', text: 'oi' })

    await waitFor(() => expect(invalidate).toHaveBeenCalled())
  })

  it('sem escritório (tenant) na sessão, não escuta nada', () => {
    signIn('Lawyer', null)
    const invalidate = mount()

    clientWrites({ atendimentoId: 'atd-004', text: '2' })

    expect(invalidate).not.toHaveBeenCalled()
  })

  it('para de escutar ao desmontar', () => {
    signIn('Lawyer')
    const queryClient = createTestQueryClient()
    const spy = vi.spyOn(queryClient, 'invalidateQueries')
    const { unmount } = renderHook(() => useAtendimentosRealtime(), {
      wrapper: ({ children }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      ),
    })

    unmount()
    clientWrites({ atendimentoId: 'atd-004', text: '2' })

    expect(spy).not.toHaveBeenCalled()
  })
})

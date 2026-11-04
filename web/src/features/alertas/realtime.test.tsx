import { renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useAuthStore } from '@/stores/authStore'

const env = vi.hoisted(() => ({ useMocks: false, isMockAuth: false, configured: true }))
vi.mock('@/config/env', () => ({
  get env() {
    return { useMocks: env.useMocks }
  },
  get isMockAuth() {
    return env.isMockAuth
  },
}))

const notify = vi.hoisted(() => ({ info: vi.fn() }))
vi.mock('@/lib/notify', () => ({ notify }))

const channelApi = vi.hoisted(() => {
  const state: { handler?: (message: { payload: unknown }) => void } = {}
  const channel = {
    on: vi.fn(
      (_type: string, _filter: unknown, handler: (message: { payload: unknown }) => void) => {
        state.handler = handler
        return channel
      }
    ),
    subscribe: vi.fn(() => channel),
  }
  const supabase = {
    realtime: { setAuth: vi.fn(() => Promise.resolve()) },
    channel: vi.fn(() => channel),
    removeChannel: vi.fn(),
  }
  return { state, channel, supabase }
})
vi.mock('@/lib/supabase/client', () => ({
  getSupabaseBrowserClient: () => channelApi.supabase,
  isSupabaseConfigured: () => env.configured,
}))

import {
  alertasChannelName,
  ALERT_CREATED_EVENT,
  MOCK_ALERT_EVENT,
  useAlertasRealtime,
} from './realtime'

function setup() {
  const queryClient = new QueryClient()
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { ...renderHook(() => useAlertasRealtime(), { wrapper }), invalidate }
}

function signIn() {
  useAuthStore.setState({
    user: {
      id: 'user-1',
      email: 'a@b.c',
      name: 'Ana',
      avatarUrl: null,
      role: 'Lawyer',
      tenantId: 'tenant-1',
    },
  })
}

beforeEach(() => {
  env.useMocks = false
  env.isMockAuth = false
  env.configured = true
  channelApi.state.handler = undefined
  useAuthStore.setState({ user: null })
})

describe('useAlertasRealtime', () => {
  it('assina o canal PRIVADO tenant:{id}:alerts:{userId}, depois de entregar o token ao Realtime', async () => {
    signIn()
    setup()

    await vi.waitFor(() => expect(channelApi.channel.subscribe).toHaveBeenCalled())

    expect(channelApi.supabase.realtime.setAuth).toHaveBeenCalled()
    expect(channelApi.supabase.channel).toHaveBeenCalledWith('tenant:tenant-1:alerts:user-1', {
      config: { private: true },
    })
    expect(channelApi.channel.on).toHaveBeenCalledWith(
      'broadcast',
      { event: ALERT_CREATED_EVENT },
      expect.any(Function)
    )
    expect(alertasChannelName('tenant-1', 'user-1')).toBe('tenant:tenant-1:alerts:user-1')
  })

  it('ao receber um alerta, mostra o toast e invalida alertas, publicações e o badge', async () => {
    signIn()
    const { invalidate } = setup()
    await vi.waitFor(() => expect(channelApi.state.handler).toBeDefined())

    channelApi.state.handler!({
      payload: {
        alertId: 'a1',
        title: 'Nova publicação: intimação',
        description: 'Processo X',
        type: 'Publication',
      },
    })

    expect(notify.info).toHaveBeenCalledWith(
      'Nova publicação: intimação',
      expect.objectContaining({ description: 'Processo X' })
    )
    const keys = invalidate.mock.calls.map(
      ([filters]) => (filters as { queryKey: string[] }).queryKey[0]
    )
    expect(keys).toEqual(expect.arrayContaining(['alerts', 'publications', 'nav-counts']))
  })

  it('ignora payload malformado — sem toast e sem invalidar', async () => {
    signIn()
    const { invalidate } = setup()
    await vi.waitFor(() => expect(channelApi.state.handler).toBeDefined())

    channelApi.state.handler!({ payload: { qualquer: 'coisa' } })
    channelApi.state.handler!({ payload: null })

    expect(notify.info).not.toHaveBeenCalled()
    expect(invalidate).not.toHaveBeenCalled()
  })

  it('remove o canal ao desmontar', async () => {
    signIn()
    const { unmount } = setup()
    await vi.waitFor(() => expect(channelApi.channel.subscribe).toHaveBeenCalled())

    unmount()

    expect(channelApi.supabase.removeChannel).toHaveBeenCalledWith(channelApi.channel)
  })

  it.each([
    ['sem sessão', () => useAuthStore.setState({ user: null })],
    [
      'em modo mock de autenticação',
      () => {
        signIn()
        env.isMockAuth = true
      },
    ],
    [
      'sem Supabase configurado',
      () => {
        signIn()
        env.configured = false
      },
    ],
  ])('não faz nada %s (o polling do resumo cobre)', async (_label, arrange) => {
    arrange()
    setup()

    await new Promise((resolve) => setTimeout(resolve, 20))

    expect(channelApi.supabase.channel).not.toHaveBeenCalled()
  })

  describe('com a API simulada (dev/E2E)', () => {
    it('não conecta ao Supabase: ouve o evento de janela que simula o canal', async () => {
      env.useMocks = true
      signIn()
      const { invalidate } = setup()

      window.dispatchEvent(
        new CustomEvent(MOCK_ALERT_EVENT, {
          detail: {
            alertId: 'novo-1',
            title: 'Nova publicação',
            description: 'Processo Y',
            type: 'Publication',
            referenceId: 'pub-1',
          },
        })
      )

      expect(channelApi.supabase.channel).not.toHaveBeenCalled()
      expect(notify.info).toHaveBeenCalledWith(
        'Nova publicação',
        expect.objectContaining({ description: 'Processo Y' })
      )
      expect(invalidate).toHaveBeenCalled()
    })

    it('ignora evento malformado e para de ouvir ao desmontar', () => {
      env.useMocks = true
      signIn()
      const { unmount } = setup()

      window.dispatchEvent(new CustomEvent(MOCK_ALERT_EVENT, { detail: { lixo: true } }))
      expect(notify.info).not.toHaveBeenCalled()

      unmount()
      window.dispatchEvent(
        new CustomEvent(MOCK_ALERT_EVENT, {
          detail: { alertId: 'x', title: 'Depois de desmontar' },
        })
      )
      expect(notify.info).not.toHaveBeenCalled()
    })
  })

  it('uma falha ao conectar nunca derruba a tela', async () => {
    signIn()
    channelApi.supabase.realtime.setAuth.mockRejectedValueOnce(new Error('sem rede'))

    expect(() => setup()).not.toThrow()
    await new Promise((resolve) => setTimeout(resolve, 20))
    expect(channelApi.supabase.channel).not.toHaveBeenCalled()
  })
})

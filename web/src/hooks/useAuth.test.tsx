import { QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook } from '@testing-library/react'
import { MOCK_PASSWORD } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { useNotificationStore } from '@/stores/notificationStore'
import { useTenantStore } from '@/stores/tenantStore'
import { routerMock } from '@/test/navigation'
import { createTestQueryClient } from '@/test/render'
import { useAuth } from './useAuth'

function setup() {
  const queryClient = createTestQueryClient()
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { queryClient, ...renderHook(() => useAuth(), { wrapper }) }
}

describe('useAuth (modo mock)', () => {
  beforeEach(() => useAuthStore.getState().clear())

  it('signIn com credenciais válidas popula o authStore', async () => {
    const { result } = setup()
    await act(() => result.current.signIn({ email: 'ana@x.com', password: MOCK_PASSWORD }))
    expect(result.current.isAuthenticated).toBe(true)
    expect(result.current.user?.email).toBe('ana@x.com')
  })

  it('signIn inválido lança erro amigável e não autentica', async () => {
    const { result } = setup()
    await expect(result.current.signIn({ email: 'ana@x.com', password: 'errada' })).rejects.toThrow(
      'E-mail ou senha inválidos.'
    )
    expect(useAuthStore.getState().isAuthenticated).toBe(false)
  })

  it('signOut limpa sessão, tenant, notificações e cache, e redireciona ao login', async () => {
    const { result, queryClient } = setup()
    await act(() => result.current.signIn({ email: 'ana@x.com', password: MOCK_PASSWORD }))
    useTenantStore.getState().setTenant({ id: 't', name: 'T', slug: 't', plan: 'trial' })
    useNotificationStore.getState().add({ kind: 'info', title: 'x' })
    queryClient.setQueryData(['secret'], 'dados do escritório')

    await act(() => result.current.signOut())

    expect(useAuthStore.getState().isAuthenticated).toBe(false)
    expect(useTenantStore.getState().currentTenant).toBeNull()
    expect(useNotificationStore.getState().items).toHaveLength(0)
    expect(queryClient.getQueryData(['secret'])).toBeUndefined()
    expect(routerMock.replace).toHaveBeenCalledWith('/login')
  })
})

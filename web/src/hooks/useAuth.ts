'use client'

import { useQueryClient } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { routes } from '@/config/routes'
import { authService } from '@/lib/auth/authService'
import type { Credentials } from '@/lib/auth/types'
import { useAuthStore } from '@/stores/authStore'
import { useNotificationStore } from '@/stores/notificationStore'
import { useTenantStore } from '@/stores/tenantStore'

export function useAuth() {
  const user = useAuthStore((s) => s.user)
  const session = useAuthStore((s) => s.session)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const isLoading = useAuthStore((s) => s.isLoading)
  const queryClient = useQueryClient()
  const router = useRouter()

  async function signIn(credentials: Credentials) {
    const newSession = await authService.signIn(credentials)
    useAuthStore.getState().setSession(newSession)
    return newSession
  }

  async function signOut() {
    try {
      await authService.signOut()
    } finally {
      // Nunca deixar dados de um escritório em cache após o logout (isolamento de tenant).
      useAuthStore.getState().clear()
      useTenantStore.getState().clear()
      useNotificationStore.getState().clear()
      queryClient.clear()
      router.replace(routes.login)
      router.refresh()
    }
  }

  return { user, session, isAuthenticated, isLoading, signIn, signOut }
}

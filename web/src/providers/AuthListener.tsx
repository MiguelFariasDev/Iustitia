'use client'

import { useEffect } from 'react'
import { authService } from '@/lib/auth/authService'
import { useAuthStore } from '@/stores/authStore'

/** Sincroniza a sessão do provedor de auth (Supabase ou mock) com o authStore. */
export function AuthListener() {
  useEffect(() => {
    let active = true
    const { setSession } = useAuthStore.getState()

    authService
      .getSession()
      .then((session) => {
        if (active) setSession(session)
      })
      .catch(() => {
        if (active) setSession(null)
      })

    let unsubscribe: (() => void) | undefined
    try {
      unsubscribe = authService.onAuthStateChange((_event, session) => setSession(session))
    } catch {
      setSession(null)
    }

    return () => {
      active = false
      unsubscribe?.()
    }
  }, [])

  return null
}

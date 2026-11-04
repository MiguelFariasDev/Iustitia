'use client'

import { useEffect } from 'react'
import { useTheme } from '@/hooks/useTheme'

/** Aplica a classe `dark` no <html> conforme o tema resolvido (claro/escuro/sistema). */
export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { resolvedTheme } = useTheme()

  useEffect(() => {
    const root = document.documentElement
    root.classList.toggle('dark', resolvedTheme === 'dark')
    root.style.colorScheme = resolvedTheme
  }, [resolvedTheme])

  return children
}

'use client'

import { Moon, Sun } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useTheme } from '@/hooks/useTheme'

/** Alternador claro/escuro das superfícies públicas (usa o mesmo store do app). */
export function ThemeToggle({ className }: { className?: string }) {
  const { resolvedTheme, toggleTheme } = useTheme()
  const dark = resolvedTheme === 'dark'

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      className={className}
      onClick={toggleTheme}
      aria-label={dark ? 'Usar tema claro' : 'Usar tema escuro'}
    >
      {dark ? <Sun aria-hidden /> : <Moon aria-hidden />}
    </Button>
  )
}

import type { ResolvedTheme, Theme } from '@/config/theme'
import { useUiStore } from '@/stores/uiStore'
import { useMediaQuery } from './useMediaQuery'

export function resolveTheme(theme: Theme, systemPrefersDark: boolean): ResolvedTheme {
  if (theme === 'system') return systemPrefersDark ? 'dark' : 'light'
  return theme
}

export function useTheme() {
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)
  const systemPrefersDark = useMediaQuery('(prefers-color-scheme: dark)')
  const resolvedTheme = resolveTheme(theme, systemPrefersDark)

  const toggleTheme = () => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')

  return { theme, resolvedTheme, setTheme, toggleTheme }
}

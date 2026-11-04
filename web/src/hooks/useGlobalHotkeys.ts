import { useEffect } from 'react'
import { useUiStore } from '@/stores/uiStore'
import { resolveTheme } from './useTheme'

function isEditableTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false
  return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)
}

/**
 * Único listener global de teclado da aplicação (regra client-event-listeners):
 *  - Ctrl/⌘ + K       → abre/fecha a paleta de comandos (funciona inclusive em inputs)
 *  - Ctrl/⌘ + Shift+L → alterna tema claro/escuro
 */
export function useGlobalHotkeys() {
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      const mod = event.metaKey || event.ctrlKey
      if (!mod) return
      const key = event.key.toLowerCase()

      if (key === 'k' && !event.shiftKey) {
        event.preventDefault()
        useUiStore.getState().toggleCommandPalette()
        return
      }
      if (key === 'l' && event.shiftKey && !isEditableTarget(event.target)) {
        event.preventDefault()
        const { theme, setTheme } = useUiStore.getState()
        const systemDark = window.matchMedia('(prefers-color-scheme: dark)').matches
        setTheme(resolveTheme(theme, systemDark) === 'dark' ? 'light' : 'dark')
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [])
}

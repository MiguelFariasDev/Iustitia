import { THEME_STORAGE_KEY } from '@/config/theme'

/**
 * Script bloqueante (inline no <head>) que aplica o tema antes da primeira pintura,
 * evitando o "flash" de tema claro. Lê o mesmo JSON persistido pelo uiStore (zustand/persist).
 */
export const themeInitScript = `(function(){try{var s=JSON.parse(localStorage.getItem('${THEME_STORAGE_KEY}')||'{}');var t=(s.state&&s.state.theme)||'system';var d=t==='dark'||(t==='system'&&matchMedia('(prefers-color-scheme: dark)').matches);var r=document.documentElement;r.classList.toggle('dark',d);r.style.colorScheme=d?'dark':'light'}catch(e){}})()`

/**
 * Espelho em TypeScript dos tokens definidos em app/globals.css, para usos fora do CSS
 * (gráficos, canvas, meta theme-color). A fonte da verdade continua sendo o CSS.
 */
export const palette = {
  primary: '#1a4fd0',
  secondary: '#1a4fd0',
  ia: '#0b6b6f',
  success: '#1b7340',
  warning: '#8f4c00',
  error: '#b3261e',
  info: '#1a4fd0',
} as const

export const themeColor = {
  light: '#f5f7fa',
  dark: '#0c1118',
} as const

/** Escala de 4px (Tailwind: 1 unidade = 0.25rem = 4px). */
export const spacing = [0, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64] as const

export const radius = { xs: 4, sm: 6, md: 8, lg: 12, xl: 12, '2xl': 20, '3xl': 20, full: 9999 } as const

/** ms. `hover` é a transição de hover; as demais, as durações de entrada (--dur-* no CSS). */
export const motion = { hover: 150, palette: 200, layer: 250, tab: 300, screen: 350 } as const

export type Theme = 'light' | 'dark' | 'system'
export type ResolvedTheme = Exclude<Theme, 'system'>

export const THEME_STORAGE_KEY = 'adv-ui'

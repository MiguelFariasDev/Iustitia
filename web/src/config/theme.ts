/**
 * Espelho em TypeScript dos tokens definidos em app/globals.css, para usos fora do CSS
 * (gráficos, canvas, meta theme-color). A fonte da verdade continua sendo o CSS.
 */
export const palette = {
  primary: '#047857',
  secondary: '#047857',
  ia: '#7c3aed',
  success: '#047857',
  warning: '#b4530a',
  error: '#c2362b',
  info: '#7c3aed',
} as const

export const themeColor = {
  light: '#f5f4ef',
  dark: '#0d1512',
} as const

/** Escala de 4px (Tailwind: 1 unidade = 0.25rem = 4px). */
export const spacing = [0, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64] as const

export const radius = { xs: 6, sm: 8, lg: 10, xl: 14, '2xl': 16, '3xl': 20, full: 9999 } as const

/** ms. `hover` é a transição de hover; as demais, as durações de entrada (--dur-* no CSS). */
export const motion = { hover: 150, palette: 200, layer: 250, tab: 300, screen: 350 } as const

export type Theme = 'light' | 'dark' | 'system'
export type ResolvedTheme = Exclude<Theme, 'system'>

export const THEME_STORAGE_KEY = 'adv-ui'

/**
 * Espelho em TypeScript dos tokens definidos em app/globals.css, para usos fora do CSS
 * (gráficos, canvas, meta theme-color). A fonte da verdade continua sendo o CSS.
 */
export const palette = {
  primary: '#0A2540',
  secondary: '#2563EB',
  gold: '#C9A961',
  success: '#10B981',
  warning: '#F59E0B',
  error: '#EF4444',
  info: '#3B82F6',
} as const

export const themeColor = {
  light: '#FFFFFF',
  dark: '#020817',
} as const

/** Escala de 4px (Tailwind: 1 unidade = 0.25rem = 4px). */
export const spacing = [0, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64] as const

export const radius = { sm: 6, md: 8, lg: 10, xl: 14, full: 9999 } as const

export const motion = { fast: 150, base: 200, slow: 300 } as const

export type Theme = 'light' | 'dark' | 'system'
export type ResolvedTheme = Exclude<Theme, 'system'>

export const THEME_STORAGE_KEY = 'adv-ui'

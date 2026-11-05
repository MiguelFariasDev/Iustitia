import { Geist } from 'next/font/google'

/**
 * Geist: tipografia das superfícies públicas (landing, cadastro, login, onboarding), no escopo
 * `.iu`. Carregada via next/font (self-hosted, sem requisição a terceiros) só pelos layouts que a
 * usam — o app autenticado continua com Inter até migrar para o v4.
 */
export const geist = Geist({ subsets: ['latin'], variable: '--font-geist', display: 'swap' })

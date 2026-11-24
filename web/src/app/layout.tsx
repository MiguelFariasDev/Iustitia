import type { Metadata, Viewport } from 'next'
import { headers } from 'next/headers'
import { IBM_Plex_Mono, Public_Sans, Source_Serif_4 } from 'next/font/google'
import { env } from '@/config/env'
import { themeColor } from '@/config/theme'
import { AppProviders } from '@/providers/AppProviders'
import { themeInitScript } from '@/providers/themeScript'
import './globals.css'

const publicSans = Public_Sans({
  subsets: ['latin'],
  variable: '--font-public-sans',
  display: 'swap',
})
const sourceSerif = Source_Serif_4({
  subsets: ['latin'],
  variable: '--font-source-serif',
  display: 'swap',
  weight: ['500', '600'],
})
const plexMono = IBM_Plex_Mono({
  subsets: ['latin'],
  variable: '--font-plex-mono',
  display: 'swap',
  weight: ['400', '500'],
})

export const metadata: Metadata = {
  title: { default: env.appName, template: `%s · ${env.appName}` },
  description: 'Gestão para escritórios de advocacia: processos, prazos, publicações e equipe.',
  applicationName: env.appName,
  robots: { index: false, follow: false },
}

export const viewport: Viewport = {
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: themeColor.light },
    { media: '(prefers-color-scheme: dark)', color: themeColor.dark },
  ],
}

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  // Nonce da CSP desta requisição (gerado em src/proxy.ts). Sem ele o script de tema seria bloqueado.
  const nonce = (await headers()).get('x-nonce') ?? undefined

  return (
    <html
      lang="pt-BR"
      className={`${publicSans.variable} ${sourceSerif.variable} ${plexMono.variable}`}
      suppressHydrationWarning
    >
      <head>
        {/* Aplica o tema antes da primeira pintura (sem flash). */}
        <script nonce={nonce} dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body>
        <a
          href="#conteudo"
          className="sr-only z-50 rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
        >
          Pular para o conteúdo
        </a>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  )
}

import { ArrowLeft, Sparkles } from 'lucide-react'
import Link from 'next/link'
import { BrandMark } from '@/components/iu/BrandMark'
import { IuScope } from '@/components/iu/IuScope'
import { ThemeToggle } from '@/components/iu/ThemeToggle'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { geist } from '@/lib/fonts'

/** Casca compartilhada de cadastro, login, recuperação de senha e convite (linguagem v4). */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <IuScope fontClass={geist.variable}>
      <div className="grid min-h-dvh lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div className="flex flex-col">
          <header className="flex items-center justify-between px-4 py-4 sm:px-8">
            <Link href={routes.home} aria-label={`${env.appName}: voltar ao site`}>
              <BrandMark className="text-lg" />
            </Link>
            <div className="flex items-center gap-1">
              <Link
                href={routes.home}
                className="hidden items-center gap-1 rounded-md px-2 py-2 text-sm text-muted-foreground hover:text-foreground sm:inline-flex"
              >
                <ArrowLeft className="size-4" aria-hidden /> Voltar ao site
              </Link>
              <ThemeToggle />
            </div>
          </header>
          <main
            id="conteudo"
            tabIndex={-1}
            className="flex flex-1 flex-col items-center justify-center px-4 py-8 outline-none sm:px-8"
          >
            {children}
          </main>
        </div>

        <aside
          aria-label={`Sobre o ${env.appName}`}
          className="hidden flex-col justify-between bg-[var(--iu-side)] p-12 text-white lg:flex"
        >
          <BrandMark tone="light" className="text-lg" />
          <div className="max-w-md space-y-8">
            <p className="text-3xl leading-tight font-semibold tracking-tight text-balance">
              Eu sugiro. Você decide.
            </p>
            <div className="rounded-[14px] border border-white/15 bg-white/5 p-4">
              <p className="mb-2 flex items-center gap-1.5 text-[11px] font-medium tracking-wide text-[#a78bfa] uppercase">
                <Sparkles className="size-3" aria-hidden /> Athena sugere
              </p>
              <p className="text-sm font-semibold">Contestação · 15 dias úteis</p>
              <p className="mt-1 text-xs text-white/60">
                Aceitar, corrigir ou rejeitar — nada vai para a agenda sem a sua decisão.
              </p>
              <p className="mt-3 text-[11px] text-white/45">Exemplo ilustrativo</p>
            </div>
          </div>
          <p className="text-xs text-white/50">
            © {new Date().getFullYear()} {env.appName}
          </p>
        </aside>
      </div>
    </IuScope>
  )
}

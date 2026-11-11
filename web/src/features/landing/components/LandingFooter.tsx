import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { env } from '@/config/env'
import { routes } from '@/config/routes'

export function LandingFooter() {
  return (
    <footer className="border-t">
      <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-10 sm:px-6 md:flex-row md:items-start md:justify-between">
        <div className="max-w-sm space-y-3">
          <Logo />
          <p className="text-xs text-muted-foreground">
            O {env.appName} é uma ferramenta de apoio e não substitui o julgamento do advogado. Os
            textos do acervo normativo têm caráter informativo e não substituem a publicação oficial.
          </p>
        </div>
        <nav aria-label="Rodapé" className="flex gap-6 text-sm">
          <Link href={routes.login} className="text-muted-foreground hover:text-foreground">Entrar</Link>
          <Link href={routes.onboarding} className="text-muted-foreground hover:text-foreground">Criar conta</Link>
        </nav>
      </div>
      <p className="border-t py-4 text-center text-xs text-muted-foreground">
        © {new Date().getFullYear()} {env.appName}
      </p>
    </footer>
  )
}

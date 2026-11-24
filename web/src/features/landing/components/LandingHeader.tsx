import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'

/** No máximo 4 links, cada um com até 3 palavras (NavBar do design system). */
const links = [
  { href: '/#como-funciona', label: 'Como funciona' },
  { href: '/#recursos', label: 'Recursos' },
  { href: '/#athena', label: 'Athena IA' },
  { href: '/#perguntas', label: 'Perguntas' },
] as const

export function LandingHeader() {
  return (
    <header className="sticky top-0 z-40 h-16 border-b bg-card">
      <div className="mx-auto flex h-full max-w-[1200px] items-center gap-8 px-4 sm:px-6">
        <Link
          href={routes.home}
          aria-label="Página inicial"
          className="rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
        >
          <Logo />
        </Link>
        <nav aria-label="Seções da página" className="hidden flex-1 items-center gap-6 md:flex">
          {links.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="text-[15px] font-medium text-foreground transition-colors hover:text-primary"
            >
              {link.label}
            </a>
          ))}
        </nav>
        <div className="ml-auto flex items-center gap-2 md:ml-0 md:gap-3">
          <Button asChild variant="secondary">
            <Link href={routes.login}>Entrar</Link>
          </Button>
          <Button asChild>
            <Link href={routes.onboarding}>Criar conta</Link>
          </Button>
        </div>
      </div>
    </header>
  )
}

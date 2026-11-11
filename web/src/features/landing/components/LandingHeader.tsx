import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'

const links = [
  { href: '/#recursos', label: 'Recursos' },
  { href: '/#ia-responsavel', label: 'IA responsável' },
  { href: '/#seguranca', label: 'Segurança' },
  { href: '/#perguntas', label: 'Perguntas' },
] as const

export function LandingHeader() {
  return (
    <header className="sticky top-0 z-40 border-b bg-background/85 backdrop-blur supports-[backdrop-filter]:bg-background/70">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <Link href={routes.home} aria-label="Página inicial" className="rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
          <Logo />
        </Link>
        <nav aria-label="Seções da página" className="hidden items-center gap-6 md:flex">
          {links.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="text-sm text-muted-foreground transition-colors hover:text-foreground"
            >
              {link.label}
            </a>
          ))}
        </nav>
        <div className="flex items-center gap-2">
          <Button asChild variant="ghost" size="sm">
            <Link href={routes.login}>Entrar</Link>
          </Button>
          <Button asChild size="sm">
            <Link href={routes.onboarding}>Criar conta</Link>
          </Button>
        </div>
      </div>
    </header>
  )
}

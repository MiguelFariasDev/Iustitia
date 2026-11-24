import { Gavel, ShieldCheck, Sparkles } from 'lucide-react'
import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { env } from '@/config/env'
import { routes } from '@/config/routes'

const highlights = [
  { icon: Gavel, text: 'Prazos e publicações sob controle, sem planilhas.', ia: false },
  { icon: Sparkles, text: 'A Athena sugere, você decide. Revisão humana sempre.', ia: true },
  { icon: ShieldCheck, text: 'Dados isolados por escritório.', ia: false },
] as const

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
      <aside
        aria-label={`Sobre o ${env.appName}`}
        className="hidden flex-col justify-between border-r bg-card p-10 lg:flex"
      >
        <Link
          href={routes.home}
          aria-label="Voltar à página inicial"
          className="w-fit rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
        >
          <Logo size={28} />
        </Link>
        <div className="space-y-8">
          <p className="display-m max-w-md text-balance">
            A gestão do seu escritório, com a seriedade que a advocacia exige.
          </p>
          <ul className="space-y-4">
            {highlights.map(({ icon: Icon, text, ia }) => (
              <li key={text} className="flex items-center gap-3 text-sm text-muted-foreground">
                <span
                  className={`flex size-8 shrink-0 items-center justify-center rounded-md ${
                    ia ? 'bg-ia-soft text-ia-text' : 'bg-brand-soft text-primary'
                  }`}
                >
                  <Icon className="size-4" aria-hidden />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>
        <p className="text-xs text-muted-foreground">
          © {new Date().getFullYear()} {env.appName}
        </p>
      </aside>
      <main
        id="conteudo"
        tabIndex={-1}
        className="flex flex-col items-center justify-center bg-background px-4 py-10 outline-none sm:px-8"
      >
        <Link
          href={routes.home}
          aria-label="Voltar à página inicial"
          className="mb-10 rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 lg:hidden"
        >
          <Logo />
        </Link>
        {children}
      </main>
    </div>
  )
}

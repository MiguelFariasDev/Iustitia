import { Gavel, ScanEye, ShieldCheck } from 'lucide-react'
import { Logo } from '@/components/shared/Logo'
import { env } from '@/config/env'

const highlights = [
  { icon: Gavel, text: 'Prazos e publicações sob controle, sem planilhas.' },
  { icon: ScanEye, text: 'IA que sugere — e você decide. Revisão humana sempre.' },
  { icon: ShieldCheck, text: 'Dados isolados por escritório, em conformidade com a LGPD.' },
]

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
      <aside
        aria-label={`Sobre o ${env.appName}`}
        className="relative hidden flex-col justify-between overflow-hidden bg-primary p-10 text-white lg:flex dark:bg-sidebar"
      >
        <div
          aria-hidden
          className="pointer-events-none absolute -top-32 -right-32 size-96 rounded-full bg-secondary/25 blur-3xl"
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -bottom-40 -left-20 size-96 rounded-full bg-gold/15 blur-3xl"
        />
        <Logo variant="inverted" size={36} className="relative" />
        <div className="relative space-y-8">
          <p className="max-w-md font-serif text-3xl leading-tight font-semibold text-balance">
            A gestão do seu escritório, com a seriedade que a advocacia exige.
          </p>
          <ul className="space-y-4">
            {highlights.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-center gap-3 text-sm text-slate-200">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white/10">
                  <Icon className="size-4 text-gold" aria-hidden />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>
        <p className="relative text-xs text-slate-300">
          © {new Date().getFullYear()} {env.appName}
        </p>
      </aside>
      <main
        id="conteudo"
        tabIndex={-1}
        className="flex flex-col items-center justify-center px-4 py-10 outline-none sm:px-8"
      >
        <Logo className="mb-10 lg:hidden" />
        {children}
      </main>
    </div>
  )
}

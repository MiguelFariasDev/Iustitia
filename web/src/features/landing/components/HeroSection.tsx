import { ArrowRight, CalendarClock, FileCheck2, ShieldCheck } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { env } from '@/config/env'
import { routes } from '@/config/routes'

export function HeroSection() {
  return (
    <section className="relative overflow-hidden bg-primary text-white dark:bg-sidebar">
      <div aria-hidden className="pointer-events-none absolute -top-40 right-0 size-[28rem] rounded-full bg-secondary/25 blur-3xl" />
      <div aria-hidden className="pointer-events-none absolute -bottom-48 -left-24 size-[28rem] rounded-full bg-gold/15 blur-3xl" />
      <div className="relative mx-auto grid max-w-6xl gap-12 px-4 py-16 sm:px-6 lg:grid-cols-[minmax(0,6fr)_minmax(0,5fr)] lg:items-center lg:py-24">
        <div className="space-y-7">
          <p className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-medium text-slate-200">
            <span className="size-1.5 rounded-full bg-gold" aria-hidden />
            Gestão para escritórios de advocacia
          </p>
          <h1 className="font-serif text-4xl leading-tight font-semibold text-balance sm:text-5xl">
            Prazos sob controle. Peças mais rápidas. A decisão é sempre sua.
          </h1>
          <p className="max-w-xl text-lg text-slate-200 text-pretty">
            O {env.appName} reúne publicações, processos, agenda e atendimento num só lugar, e usa
            inteligência artificial como apoio, com revisão humana em cada etapa.
          </p>
          <div className="flex flex-col gap-3 sm:flex-row">
            <Button asChild size="lg" className="bg-gold text-gold-foreground hover:bg-gold/90">
              <Link href={routes.onboarding}>
                Cadastre seu escritório <ArrowRight aria-hidden />
              </Link>
            </Button>
            <Button
              asChild
              size="lg"
              variant="outline"
              className="border-white/30 bg-transparent text-white hover:bg-white/10 hover:text-white"
            >
              <Link href={routes.login}>Já tenho conta</Link>
            </Button>
          </div>
          {env.soloSignupEnabled ? (
            <p className="text-sm text-slate-300">
              Advogado autônomo? O cadastro também atende quem trabalha sozinho.
            </p>
          ) : null}
        </div>
        <ProductPreview />
      </div>
    </section>
  )
}

/** Ilustração do produto — dados fictícios, só para dar ideia da tela. */
function ProductPreview() {
  return (
    <figure aria-label="Ilustração da tela do produto, com dados fictícios" className="relative">
      <div className="space-y-3 rounded-2xl border border-white/15 bg-white/10 p-4 shadow-2xl backdrop-blur">
        <div className="flex items-center gap-2 text-xs text-slate-300" aria-hidden>
          <span className="size-2.5 rounded-full bg-red-400/80" />
          <span className="size-2.5 rounded-full bg-amber-300/80" />
          <span className="size-2.5 rounded-full bg-emerald-400/80" />
          <span className="ml-2">Painel · exemplo</span>
        </div>
        <PreviewCard
          icon={<CalendarClock className="size-4 text-gold" aria-hidden />}
          title="Prazo em 2 dias"
          text="Contestação · processo de exemplo"
          tag="Urgente"
          tagClass="bg-red-500/20 text-red-100"
        />
        <PreviewCard
          icon={<ShieldCheck className="size-4 text-gold" aria-hidden />}
          title="Publicação classificada"
          text="Sugestão da IA aguardando a sua confirmação"
          tag="Revisar"
          tagClass="bg-amber-400/20 text-amber-100"
        />
        <PreviewCard
          icon={<FileCheck2 className="size-4 text-gold" aria-hidden />}
          title="Minuta em revisão"
          text="Citações com fonte · exportação após conferência"
          tag="Rascunho"
          tagClass="bg-sky-400/20 text-sky-100"
        />
      </div>
      <figcaption className="mt-3 text-center text-xs text-slate-300">
        Ilustração com dados fictícios.
      </figcaption>
    </figure>
  )
}

function PreviewCard({
  icon,
  title,
  text,
  tag,
  tagClass,
}: {
  icon: React.ReactNode
  title: string
  text: string
  tag: string
  tagClass: string
}) {
  return (
    <div className="flex items-start gap-3 rounded-xl bg-white/10 p-3">
      <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg bg-white/10">{icon}</span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs text-slate-300">{text}</p>
      </div>
      <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs ${tagClass}`}>{tag}</span>
    </div>
  )
}

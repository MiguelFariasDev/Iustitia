import { Check, Scale } from 'lucide-react'
import Link from 'next/link'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { faq, features, responsibleAi, security, statusLabel, steps } from '../content'

function SectionHeading({ eyebrow, title, intro }: { eyebrow: string; title: string; intro?: string }) {
  return (
    <div className="mx-auto max-w-2xl space-y-3 text-center">
      <p className="text-sm font-semibold tracking-wide text-secondary uppercase">{eyebrow}</p>
      <h2 className="font-serif text-3xl font-semibold text-balance sm:text-4xl">{title}</h2>
      {intro ? <p className="text-muted-foreground text-pretty">{intro}</p> : null}
    </div>
  )
}

export function HowItWorksSection() {
  return (
    <section aria-labelledby="como-funciona" className="mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:py-20">
      <SectionHeading eyebrow="Como funciona" title="Da publicação ao prazo na agenda" />
      <h3 id="como-funciona" className="sr-only">Passos</h3>
      <ol className="mt-12 grid gap-6 md:grid-cols-3">
        {steps.map((step, index) => (
          <li key={step.title} className="space-y-3 rounded-xl border bg-card p-6">
            <span className="flex size-9 items-center justify-center rounded-full bg-primary font-serif text-lg font-semibold text-primary-foreground dark:bg-secondary">
              {index + 1}
            </span>
            <p className="text-lg font-semibold">{step.title}</p>
            <p className="text-sm text-muted-foreground">{step.description}</p>
          </li>
        ))}
      </ol>
    </section>
  )
}

export function FeaturesSection() {
  return (
    <section id="recursos" aria-labelledby="recursos-titulo" className="scroll-mt-20 bg-muted/50 py-16 lg:py-20">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <SectionHeading
          eyebrow="Recursos"
          title="Tudo o que o escritório usa no dia a dia"
          intro="Cada módulo se conversa: o cliente, o processo, o prazo e a minuta ficam ligados."
        />
        <h3 id="recursos-titulo" className="sr-only">Lista de recursos</h3>
        <ul className="mt-12 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {features.map(({ id, icon: Icon, title, description, status }) => (
            <li key={id} className="flex flex-col gap-3 rounded-xl border bg-card p-6">
              <div className="flex items-center justify-between gap-3">
                <span className="flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary dark:bg-secondary/20 dark:text-secondary-foreground">
                  <Icon className="size-5" aria-hidden />
                </span>
                {statusLabel[status] ? <Badge variant="outline">{statusLabel[status]}</Badge> : null}
              </div>
              <p className="font-semibold">{title}</p>
              <p className="text-sm text-muted-foreground">{description}</p>
            </li>
          ))}
        </ul>
        <p className="mt-6 text-center text-sm text-muted-foreground">
          Recursos marcados como <strong>Beta</strong> ou <strong>Em evolução</strong> estão em
          construção e podem mudar.
        </p>
      </div>
    </section>
  )
}

export function ResponsibleAiSection() {
  return (
    <section id="ia-responsavel" aria-labelledby="ia-titulo" className="scroll-mt-20 bg-primary text-white dark:bg-sidebar">
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-16 sm:px-6 lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)] lg:py-20">
        <div className="space-y-4">
          <p className="text-sm font-semibold tracking-wide text-gold uppercase">IA responsável</p>
          <h2 id="ia-titulo" className="font-serif text-3xl font-semibold text-balance sm:text-4xl">
            Inteligência artificial que apoia, não que decide
          </h2>
          <p className="text-slate-200 text-pretty">
            Na advocacia, a responsabilidade é do profissional. Por isso a IA do {env.appName} foi
            desenhada para mostrar de onde vem cada afirmação e para exigir a sua conferência.
          </p>
        </div>
        <ul className="grid gap-4 sm:grid-cols-2">
          {responsibleAi.map((item) => (
            <li key={item.title} className="space-y-2 rounded-xl border border-white/15 bg-white/5 p-5">
              <p className="flex items-center gap-2 font-semibold">
                <Check className="size-4 text-gold" aria-hidden /> {item.title}
              </p>
              <p className="text-sm text-slate-300">{item.description}</p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}

export function SecuritySection() {
  return (
    <section id="seguranca" aria-labelledby="seguranca-titulo" className="mx-auto max-w-6xl scroll-mt-20 px-4 py-16 sm:px-6 lg:py-20">
      <SectionHeading
        eyebrow="Segurança"
        title="Os dados do seu escritório, protegidos"
        intro="Informação de cliente é sigilosa. O sistema trata isso como requisito, não como detalhe."
      />
      <h3 id="seguranca-titulo" className="sr-only">Medidas de segurança</h3>
      <ul className="mt-12 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {security.map((item) => (
          <li key={item.title} className="space-y-2 rounded-xl border bg-card p-5">
            <p className="font-semibold">{item.title}</p>
            <p className="text-sm text-muted-foreground">{item.description}</p>
          </li>
        ))}
      </ul>
    </section>
  )
}

export function AudienceSection() {
  return (
    <section aria-labelledby="para-quem" className="bg-muted/50 py-16 lg:py-20">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <SectionHeading eyebrow="Para quem é" title="Do advogado autônomo ao escritório com equipe" />
        <h3 id="para-quem" className="sr-only">Tipos de conta</h3>
        <div className="mt-12 grid gap-6 md:grid-cols-2">
          <div className="space-y-4 rounded-xl border bg-card p-6">
            <p className="font-serif text-xl font-semibold">Escritório</p>
            <p className="text-sm text-muted-foreground">
              Equipe com sócios, advogados e apoio, perfis de acesso e tudo organizado por escritório.
            </p>
            <Button asChild>
              <Link href={routes.onboarding}>Cadastre seu escritório</Link>
            </Button>
          </div>
          {env.soloSignupEnabled ? (
            <div className="space-y-4 rounded-xl border bg-card p-6">
              <p className="font-serif text-xl font-semibold">Advogado autônomo</p>
              <p className="text-sm text-muted-foreground">
                Para quem trabalha sozinho: prazos, processos e minutas sem a complexidade de uma equipe.
              </p>
              <Button asChild variant="outline">
                <Link href={routes.onboarding}>Crie sua conta</Link>
              </Button>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  )
}

export function FaqSection() {
  return (
    <section id="perguntas" aria-labelledby="perguntas-titulo" className="mx-auto max-w-3xl scroll-mt-20 px-4 py-16 sm:px-6 lg:py-20">
      <SectionHeading eyebrow="Perguntas" title="Perguntas frequentes" />
      <h3 id="perguntas-titulo" className="sr-only">Lista de perguntas</h3>
      <div className="mt-10 divide-y rounded-xl border bg-card">
        {faq.map((item) => (
          <details key={item.q} className="group p-5">
            <summary className="cursor-pointer list-none font-medium outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 [&::-webkit-details-marker]:hidden">
              <span className="flex items-center justify-between gap-4">
                {item.q}
                <span aria-hidden className="text-muted-foreground transition-transform group-open:rotate-45">+</span>
              </span>
            </summary>
            <p className="mt-3 text-sm text-muted-foreground">{item.a}</p>
          </details>
        ))}
      </div>
    </section>
  )
}

export function FinalCtaSection() {
  return (
    <section aria-labelledby="cta-final" className="px-4 pb-16 sm:px-6 lg:pb-20">
      <div className="mx-auto max-w-4xl space-y-6 rounded-2xl bg-primary p-10 text-center text-white dark:bg-sidebar">
        <Scale className="mx-auto size-8 text-gold" aria-hidden />
        <h2 id="cta-final" className="font-serif text-3xl font-semibold text-balance">
          Comece a organizar o seu escritório
        </h2>
        <div className="flex flex-col justify-center gap-3 sm:flex-row">
          <Button asChild size="lg" className="bg-gold text-gold-foreground hover:bg-gold/90">
            <Link href={routes.onboarding}>Criar conta</Link>
          </Button>
          <Button asChild size="lg" variant="outline" className="border-white/30 bg-transparent text-white hover:bg-white/10 hover:text-white">
            <Link href={routes.login}>Entrar</Link>
          </Button>
        </div>
      </div>
    </section>
  )
}

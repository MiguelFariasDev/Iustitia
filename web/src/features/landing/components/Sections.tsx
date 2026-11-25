import {
  Building2,
  CalendarCheck,
  CircleCheck,
  History,
  Inbox,
  ShieldCheck,
  Sparkles,
  UserCog,
  type LucideIcon,
} from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { faq, features, responsibleAi, security, statusLabel, steps } from '../content'

/** Seção da landing: 96px entre seções (`py-24`), conteúdo em até 1200px, fundos alternados. */
function Section({
  id,
  titleId,
  tone = 'background',
  children,
}: {
  id?: string
  titleId: string
  tone?: 'background' | 'card'
  children: React.ReactNode
}) {
  return (
    <section
      id={id}
      aria-labelledby={titleId}
      className={`scroll-mt-16 px-4 py-16 sm:px-6 lg:py-24 ${tone === 'card' ? 'bg-card' : 'bg-background'}`}
    >
      <div className="mx-auto max-w-[1200px]">{children}</div>
    </section>
  )
}

function SectionHeading({
  id,
  eyebrow,
  title,
  intro,
  tone = 'brand',
}: {
  id: string
  eyebrow: string
  title: string
  intro?: string
  tone?: 'brand' | 'ia'
}) {
  return (
    <div className="mb-12 max-w-[640px]">
      <p className={`iu-label mb-3 ${tone === 'ia' ? 'text-ia-text' : 'text-primary'}`}>{eyebrow}</p>
      <h2 id={id} className="display-l text-foreground">
        {title}
      </h2>
      {intro ? <p className="mt-4 text-lg leading-[29px] text-muted-foreground">{intro}</p> : null}
    </div>
  )
}

function IconBox({ icon: Icon, tone = 'brand' }: { icon: LucideIcon; tone?: 'brand' | 'ia' }) {
  return (
    <div
      className={`mb-5 grid size-11 place-items-center rounded-md ${
        tone === 'ia' ? 'bg-ia-soft text-ia-text' : 'bg-brand-soft text-primary'
      }`}
    >
      <Icon className="size-[18px]" aria-hidden />
    </div>
  )
}

const stepIcons: LucideIcon[] = [Inbox, Sparkles, CalendarCheck]

export function HowItWorksSection() {
  return (
    <Section id="como-funciona" titleId="como-funciona-titulo">
      <SectionHeading
        id="como-funciona-titulo"
        eyebrow="Como funciona"
        title="Da publicação ao prazo na agenda"
      />
      <ol className="grid gap-6 md:grid-cols-3">
        {steps.map((step, index) => (
          <li key={step.title} className="rounded-lg border bg-card p-8">
            <IconBox icon={stepIcons[index] ?? Inbox} tone={index === 1 ? 'ia' : 'brand'} />
            <p className="mb-2 text-[17px] leading-6 font-semibold">{step.title}</p>
            <p className="text-muted-foreground">{step.description}</p>
          </li>
        ))}
      </ol>
    </Section>
  )
}

export function FeaturesSection() {
  return (
    <Section id="recursos" titleId="recursos-titulo" tone="card">
      <SectionHeading
        id="recursos-titulo"
        eyebrow="Recursos"
        title="Tudo o que o escritório usa no dia a dia"
        intro="Cada módulo se conversa: o cliente, o processo, o prazo e a minuta ficam ligados."
      />
      <ul className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {features.map(({ id, icon, title, description, status }) => (
          <li key={id} className="flex flex-col rounded-lg border bg-background p-8">
            <div className="flex items-start justify-between gap-3">
              <IconBox icon={icon} tone={id === 'pecas' ? 'ia' : 'brand'} />
              {statusLabel[status] ? (
                <span className="iu-label inline-flex h-6 items-center rounded-sm bg-muted px-2 text-muted-foreground">
                  {statusLabel[status]}
                </span>
              ) : null}
            </div>
            <p className="mb-2 text-[17px] leading-6 font-semibold">{title}</p>
            <p className="text-muted-foreground">{description}</p>
          </li>
        ))}
      </ul>
      <p className="mt-6 text-sm text-muted-foreground">
        Recursos marcados como <strong className="font-semibold">Beta</strong> ou{' '}
        <strong className="font-semibold">Em evolução</strong> estão em construção e podem mudar.
      </p>
    </Section>
  )
}

/** A cor da Athena (verde-azulado) é exclusiva da IA: só esta seção e os selos de IA a usam. */
export function ResponsibleAiSection() {
  return (
    <Section id="athena" titleId="athena-titulo">
      <div className="rounded-2xl border border-ia-border bg-ia-soft p-8 sm:p-12">
        <SectionHeading
          id="athena-titulo"
          tone="ia"
          eyebrow="Athena IA"
          title="Inteligência artificial que sugere, e você decide"
          intro={`Na advocacia, a responsabilidade é do profissional. Por isso a Athena do ${env.appName} mostra de onde vem cada afirmação e exige a sua conferência.`}
        />
        <ul className="grid gap-4 sm:grid-cols-2">
          {responsibleAi.map((item) => (
            <li key={item.title} className="rounded-lg border border-ia-border bg-card p-6">
              <p className="mb-2 flex items-center gap-2 font-semibold text-foreground">
                <CircleCheck className="size-[18px] text-ia-text" aria-hidden /> {item.title}
              </p>
              <p className="text-sm text-muted-foreground">{item.description}</p>
            </li>
          ))}
        </ul>
      </div>
    </Section>
  )
}

const securityIcons: LucideIcon[] = [Building2, UserCog, History, ShieldCheck]

export function SecuritySection() {
  return (
    <Section id="seguranca" titleId="seguranca-titulo" tone="card">
      <SectionHeading
        id="seguranca-titulo"
        eyebrow="Segurança"
        title="Os dados do seu escritório, protegidos"
        intro="Informação de cliente é sigilosa. O sistema trata isso como requisito, não como detalhe."
      />
      <ul className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {security.map((item, index) => (
          <li key={item.title} className="rounded-lg border bg-background p-6">
            <IconBox icon={securityIcons[index] ?? ShieldCheck} />
            <p className="mb-2 font-semibold">{item.title}</p>
            <p className="text-sm text-muted-foreground">{item.description}</p>
          </li>
        ))}
      </ul>
    </Section>
  )
}

export function AudienceSection() {
  return (
    <Section titleId="para-quem-titulo">
      <SectionHeading
        id="para-quem-titulo"
        eyebrow="Para quem é"
        title="Do advogado autônomo ao escritório com equipe"
      />
      <div className="grid gap-6 md:grid-cols-2">
        <div className="space-y-4 rounded-lg border bg-card p-8">
          <p className="display-m">Escritório</p>
          <p className="text-muted-foreground">
            Equipe com sócios, advogados e apoio, perfis de acesso e tudo organizado por escritório.
          </p>
          <Button asChild>
            <Link href={routes.onboarding}>Cadastre seu escritório</Link>
          </Button>
        </div>
        {env.soloSignupEnabled ? (
          <div className="space-y-4 rounded-lg border bg-card p-8">
            <p className="display-m">Advogado autônomo</p>
            <p className="text-muted-foreground">
              Para quem trabalha sozinho: prazos, processos e minutas sem a complexidade de uma equipe.
            </p>
            <Button asChild variant="secondary">
              <Link href={routes.onboarding}>Crie sua conta</Link>
            </Button>
          </div>
        ) : null}
      </div>
    </Section>
  )
}

export function FaqSection() {
  return (
    <Section id="perguntas" titleId="perguntas-titulo" tone="card">
      <div className="mx-auto max-w-3xl">
        <SectionHeading id="perguntas-titulo" eyebrow="Perguntas" title="Perguntas frequentes" />
        <div className="divide-y rounded-lg border bg-background">
          {faq.map((item) => (
            <details key={item.q} className="group p-6">
              <summary className="cursor-pointer list-none font-semibold outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 [&::-webkit-details-marker]:hidden">
                <span className="flex items-center justify-between gap-4">
                  {item.q}
                  <span
                    aria-hidden
                    className="text-xl leading-none text-muted-foreground transition-transform group-open:rotate-45"
                  >
                    +
                  </span>
                </span>
              </summary>
              <p className="mt-3 text-muted-foreground">{item.a}</p>
            </details>
          ))}
        </div>
      </div>
    </Section>
  )
}

/** Faixa de CTA: brand-050 sobre surface-000, uma única ação primária. */
export function FinalCtaSection() {
  return (
    <section aria-labelledby="cta-final" className="bg-card px-4 py-16 sm:px-6">
      <div className="mx-auto flex max-w-[1200px] flex-col items-start justify-between gap-8 rounded-xl bg-brand-soft p-8 sm:p-12 md:flex-row md:items-center">
        <div>
          <h2 id="cta-final" className="display-m mb-2 text-foreground">
            Comece pelos prazos do seu escritório
          </h2>
          <p className="text-lg leading-[29px] text-muted-foreground">
            Cadastre o escritório e traga a agenda, as publicações e a equipe para um só painel.
          </p>
        </div>
        <Button asChild size="lg" className="h-[52px] shrink-0 px-8 text-base">
          <Link href={routes.onboarding}>Criar conta</Link>
        </Button>
      </div>
    </section>
  )
}

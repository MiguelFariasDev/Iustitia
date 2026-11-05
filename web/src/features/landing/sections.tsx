import { ArrowRight, Lock, ShieldCheck } from 'lucide-react'
import Link from 'next/link'
import { BrandMark } from '@/components/iu/BrandMark'
import { Reveal } from '@/components/iu/Reveal'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { env } from '@/config/env'
import { cn } from '@/lib/utils'
import { faqs, features, securityPoints, steps } from './content'
import { HeroDemo } from './HeroDemo'
import { RolesTabs } from './RolesTabs'

function Section({
  id,
  title,
  eyebrow,
  children,
  className,
}: {
  id: string
  title: string
  eyebrow?: string
  children: React.ReactNode
  className?: string
}) {
  return (
    <section id={id} aria-labelledby={`${id}-titulo`} className={cn('scroll-mt-20 py-16 sm:py-24', className)}>
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Reveal className="mb-10 max-w-2xl">
          {eyebrow ? <p className="iu-mono mb-2 text-xs text-muted-foreground">{eyebrow}</p> : null}
          <h2 id={`${id}-titulo`} className="text-3xl font-semibold text-balance sm:text-4xl">
            {title}
          </h2>
        </Reveal>
        {children}
      </div>
    </section>
  )
}

export function Hero() {
  return (
    <section aria-labelledby="hero-titulo" className="pt-12 pb-16 sm:pt-20 sm:pb-24">
      <div className="mx-auto grid max-w-6xl items-center gap-12 px-4 sm:px-6 lg:grid-cols-[1.05fr_1fr]">
        <div className="iu-in">
          <p className="iu-mono mb-4 text-xs text-muted-foreground">
            DJEN/CNJ · prazos · revisão humana
          </p>
          <h1 id="hero-titulo" className="text-4xl leading-[1.05] font-semibold text-balance sm:text-6xl">
            Nenhum prazo escapa. Você continua decidindo.
          </h1>
          <p className="mt-5 max-w-xl text-lg leading-relaxed text-muted-foreground">
            O {env.appName} monitora as publicações do DJEN/CNJ, a Athena sugere o prazo com o
            fundamento legal e o advogado revisa cada sugestão antes de qualquer coisa ir para a
            agenda.
          </p>
          <div className="mt-8 flex flex-wrap items-center gap-3">
            <Button asChild className="iu-btn-lg">
              <Link href={routes.signup}>
                Começar grátis <ArrowRight aria-hidden />
              </Link>
            </Button>
            <Button asChild variant="outline" className="iu-btn-lg">
              <a href="#como-funciona">Ver como funciona</a>
            </Button>
          </div>
          <p className="mt-4 text-sm text-muted-foreground">
            Projeto de portfólio · sem cobrança · feito para escritórios pequenos
          </p>
        </div>
        <div className="iu-in" style={{ animationDelay: '120ms' }}>
          <HeroDemo />
        </div>
      </div>
    </section>
  )
}

export function HowItWorks() {
  return (
    <Section id="como-funciona" eyebrow="COMO FUNCIONA" title="Da publicação à agenda, em três passos">
      <ol className="grid gap-4 md:grid-cols-3">
        {steps.map((step, i) => (
          <Reveal as="li" index={i} key={step.n} className="iu-card iu-lift p-6">
            <span className="iu-mono text-sm text-[var(--iu-brand)]">{step.n}</span>
            <h3 className="mt-3 text-lg font-semibold">{step.title}</h3>
            <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{step.text}</p>
          </Reveal>
        ))}
      </ol>
    </Section>
  )
}

export function Principle() {
  return (
    <section aria-labelledby="principio-titulo" className="py-6">
      <Reveal className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="rounded-2xl bg-[var(--iu-side)] p-8 text-white sm:p-12">
          <ShieldCheck className="mb-4 size-6 text-[#34d399]" aria-hidden />
          <h2 id="principio-titulo" className="max-w-2xl text-2xl font-semibold text-balance sm:text-3xl">
            Responsabilidade final do advogado — nada é salvo sem sua decisão.
          </h2>
          <p className="mt-4 max-w-2xl leading-relaxed text-white/75">
            A revisão humana não é uma limitação do produto: é o produto. Toda sugestão da IA espera
            a sua aceitação, correção ou rejeição, e cada decisão fica registrada. É o que o
            Provimento CFOAB nº 271/2025 espera e o que a sua responsabilidade profissional exige.
          </p>
        </div>
      </Reveal>
    </section>
  )
}

export function Features() {
  return (
    <Section id="recursos" eyebrow="RECURSOS" title="O que o escritório usa no dia a dia">
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {features.map((feature, i) => {
          const Icon = feature.icon
          return (
            <Reveal as="li" index={i % 3} key={feature.title} className="iu-card iu-lift p-6">
              <div className="flex items-start justify-between gap-3">
                <span className="flex size-10 items-center justify-center rounded-[10px] bg-[var(--iu-brand-soft)] text-[var(--iu-brand)]">
                  <Icon className="size-5" strokeWidth={1.5} aria-hidden />
                </span>
                {feature.status ? (
                  <span className="rounded-full border px-2 py-0.5 text-[11px] text-muted-foreground">
                    {feature.status}
                  </span>
                ) : null}
              </div>
              <h3 className="mt-4 font-semibold">{feature.title}</h3>
              <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">{feature.text}</p>
            </Reveal>
          )
        })}
      </ul>
    </Section>
  )
}

export function Roles() {
  return (
    <Section id="perfis" eyebrow="PARA CADA PERFIL" title="Cada pessoa vê o que o trabalho dela pede">
      <Reveal>
        <RolesTabs />
      </Reveal>
    </Section>
  )
}

export function Security() {
  return (
    <Section id="seguranca" eyebrow="SEGURANÇA E LGPD" title="Sigilo profissional levado a sério">
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {securityPoints.map((point, i) => (
          <Reveal as="li" index={i % 3} key={point.title} className="iu-card p-6">
            <Lock className="mb-3 size-4 text-[var(--iu-brand)]" aria-hidden />
            <h3 className="font-semibold">{point.title}</h3>
            <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">{point.text}</p>
          </Reveal>
        ))}
      </ul>
      <p className="mt-6 text-sm text-muted-foreground">
        Detalhes sobre tratamento de dados na{' '}
        <Link href={routes.privacy} className="text-[var(--iu-brand)] underline underline-offset-4">
          Política de Privacidade
        </Link>
        .
      </p>
    </Section>
  )
}

export function Pricing() {
  return (
    <Section id="precos" eyebrow="PREÇOS" title="Sem cobrança: é um projeto de portfólio">
      <Reveal className="iu-card max-w-2xl p-8">
        {/*
          Enquadramento obrigatório (docs/compliance/portfolio.md, ADR-064): o Termo de Uso do DataJud
          proíbe uso comercial. Enquanto ele estiver integrado NÃO pode haver planos, preços nem cobrança.
          {{CONFIRMAR}} o modelo de acesso (convite? cadastro aberto?) antes de divulgar esta página.
        */}
        <p className="text-lg font-semibold">Não há planos, preços nem cobrança</p>
        <p className="mt-2 leading-relaxed text-muted-foreground">
          O {env.appName} é uma demonstração técnica, não é comercializado e não é oferecido como
          serviço a terceiros. O uso por um escritório é um teste de usabilidade, sem contrapartida
          financeira.
        </p>
        <p className="mt-3 text-xs text-muted-foreground">
          {'{{CONFIRMAR}}'} modelo de acesso após a decisão sobre o DataJud (docs/compliance/portfolio.md)
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <Button asChild className="iu-btn-lg">
            <Link href={routes.signup}>Começar grátis</Link>
          </Button>
        </div>
      </Reveal>
    </Section>
  )
}

export function Faq() {
  return (
    <Section id="perguntas" eyebrow="PERGUNTAS" title="Perguntas frequentes">
      <div className="max-w-3xl divide-y rounded-[14px] border bg-card">
        {faqs.map((item) => (
          <details key={item.q} className="group px-5 py-4">
            <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-4 font-medium [&::-webkit-details-marker]:hidden">
              {item.q}
              <span aria-hidden className="text-muted-foreground transition-transform group-open:rotate-45">
                +
              </span>
            </summary>
            <p className="pt-2 pb-1 text-sm leading-relaxed text-muted-foreground">{item.a}</p>
          </details>
        ))}
      </div>
    </Section>
  )
}

export function FinalCta() {
  return (
    <section aria-labelledby="cta-titulo" className="py-16">
      <Reveal className="mx-auto max-w-3xl px-4 text-center sm:px-6">
        <h2 id="cta-titulo" className="text-3xl font-semibold text-balance sm:text-4xl">
          Comece hoje. Você continua decidindo.
        </h2>
        <div className="mt-8">
          <Button asChild className="iu-btn-lg">
            <Link href={routes.signup}>
              Começar grátis <ArrowRight aria-hidden />
            </Link>
          </Button>
        </div>
      </Reveal>
    </section>
  )
}

export function Footer() {
  return (
    <footer className="border-t py-10">
      <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 sm:px-6 md:flex-row md:items-center md:justify-between">
        <BrandMark />
        <nav aria-label="Rodapé" className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-muted-foreground">
          <a href="#como-funciona" className="hover:text-foreground">Produto</a>
          <a href="#seguranca" className="hover:text-foreground">Segurança</a>
          <Link href={routes.terms} className="hover:text-foreground">Termos de uso</Link>
          <Link href={routes.privacy} className="hover:text-foreground">Política de privacidade</Link>
          <span>{'{{PREENCHER}}'} contato</span>
        </nav>
        <p className="text-xs text-muted-foreground">
          © {new Date().getFullYear()} {env.appName}
        </p>
      </div>
      {/* Mesmo aviso do rodapé do app (ADR-064). Não remover. */}
      <p className="mx-auto mt-6 max-w-6xl px-4 text-xs text-muted-foreground sm:px-6">
        Projeto de portfólio. Não comercializado. Dados de processos obtidos do DataJud (CNJ) sob os
        termos de uso da API Pública.
      </p>
    </footer>
  )
}

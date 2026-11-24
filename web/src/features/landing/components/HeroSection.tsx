import { ArrowRight, CircleCheck, Sparkles } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { env } from '@/config/env'
import { routes } from '@/config/routes'

/** Três benefícios, só o que o produto faz hoje (sem prometer portal, resultado ou número). */
const benefits = [
  {
    title: 'Tudo em um só lugar:',
    text: 'processos, prazos, agenda e clientes no mesmo painel, sem planilha paralela.',
  },
  {
    title: 'Triagem com a Athena:',
    text: 'a IA sugere a classificação e o prazo de cada publicação; você revisa e confirma.',
  },
  {
    title: 'Revisão humana sempre:',
    text: 'nada vira prazo, tarefa ou peça sem o seu clique.',
  },
] as const

/**
 * Primeira dobra (Hero do design system): selo da Athena, título em serifa, benefícios, dois CTAs
 * e um mockup do produto recriado com os mesmos componentes. Sem gradiente, foto de banco de
 * imagens, balança ou martelo.
 */
export function HeroSection() {
  return (
    <section aria-labelledby="hero-titulo" className="overflow-hidden bg-card">
      <div className="mx-auto grid max-w-[1200px] items-center gap-12 px-4 py-12 sm:px-6 lg:grid-cols-[1fr_1.05fr] lg:py-16">
        <div>
          <p className="mb-4">
            <span className="iu-label inline-flex h-6 items-center gap-1 rounded-sm bg-ia-soft px-2 text-ia-text">
              <Sparkles className="size-3.5" aria-hidden />
              Com a Athena, IA com revisão humana
            </span>
          </p>
          <h1 id="hero-titulo" className="display-xl mb-8 text-foreground">
            Você advoga. A&nbsp;{env.appName} organiza.
          </h1>
          <ul className="mb-8 grid gap-5">
            {benefits.map((benefit) => (
              <li key={benefit.title} className="grid grid-cols-[24px_1fr] gap-3 text-[17px] leading-[27px] text-muted-foreground">
                <CircleCheck className="mt-[3px] size-[22px] text-primary" aria-hidden />
                <span>
                  <strong className="font-semibold text-foreground">{benefit.title}</strong>{' '}
                  {benefit.text}
                </span>
              </li>
            ))}
          </ul>
          <div className="flex flex-wrap gap-3">
            <Button asChild size="lg" className="h-[52px] px-8 text-base">
              <Link href={routes.onboarding}>
                Cadastre seu escritório <ArrowRight aria-hidden />
              </Link>
            </Button>
            <Button asChild size="lg" variant="secondary" className="h-[52px] px-8 text-base">
              <a href="#como-funciona">Ver como funciona</a>
            </Button>
          </div>
          <p className="mt-4 text-[13px] text-muted-foreground">
            Seus dados ficam isolados por escritório.
            {env.soloSignupEnabled ? ' O cadastro também atende advogado autônomo.' : ''}
          </p>
        </div>
        <ProductPreview />
      </div>
    </section>
  )
}

/** Mockup com dados FICTÍCIOS (a legenda abaixo diz isso). Decorativo para leitores de tela. */
function ProductPreview() {
  return (
    <figure className="relative m-0">
      <div
        aria-hidden
        className="relative rounded-2xl bg-muted px-4 py-10 sm:px-8 sm:py-12 lg:min-h-[440px]"
      >
        <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
          <div className="flex h-8 items-center gap-2 border-b bg-background px-3">
            <span className="size-2 rounded-full bg-input" />
            <span className="size-2 rounded-full bg-input" />
            <span className="size-2 rounded-full bg-input" />
          </div>
          <div className="grid gap-3 p-4">
            <div className="grid grid-cols-[auto_1fr] items-start gap-3 rounded-lg border border-ia-border bg-ia-soft p-3">
              <span className="grid size-8 place-items-center rounded-md bg-card text-ia-text">
                <Sparkles className="size-[18px]" />
              </span>
              <div>
                <p className="iu-label mb-0.5 text-ia-text">Sugestão da Athena</p>
                <p className="text-sm leading-[21px] text-foreground">
                  Intimação para contrarrazões. Prazo sugerido: 15 dias úteis.
                </p>
              </div>
            </div>
            <table className="w-full border-separate border-spacing-0 text-sm">
              <thead>
                <tr>
                  {['Divulgado', 'Tipo', 'Status'].map((head) => (
                    <th
                      key={head}
                      className="iu-label border-b bg-muted px-4 py-2 text-left text-muted-foreground"
                    >
                      {head}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                <Row data="13/11" tipo="Intimação" status="Não tratada" tom="pendente" />
                <Row data="12/11" tipo="Despacho" status="Tratada" tom="feito" />
                <Row data="12/11" tipo="Sentença" status="Prazo fatal" tom="fatal" ultima />
              </tbody>
            </table>
          </div>
        </div>
        <div className="absolute top-5 left-2 hidden min-w-[168px] rounded-lg border bg-card px-5 py-4 shadow-lg sm:block lg:-left-7">
          <p className="iu-label text-muted-foreground">Não tratadas hoje</p>
          <p className="iu-metric text-warning">4</p>
        </div>
        <div className="absolute right-2 -bottom-6 hidden min-w-[168px] rounded-lg border bg-card px-5 py-4 shadow-lg sm:block lg:-right-5">
          <p className="iu-label text-muted-foreground">Prazos da semana</p>
          <p className="iu-metric text-primary">9</p>
        </div>
      </div>
      <figcaption className="mt-8 text-center text-xs text-muted-foreground">
        Ilustração com dados fictícios.
      </figcaption>
    </figure>
  )
}

const tons = {
  pendente: 'bg-warning-soft text-warning',
  feito: 'bg-success-soft text-success',
  fatal: 'bg-destructive-soft text-destructive',
} as const

function Row({
  data,
  tipo,
  status,
  tom,
  ultima = false,
}: {
  data: string
  tipo: string
  status: string
  tom: keyof typeof tons
  ultima?: boolean
}) {
  const cell = ultima ? 'px-4 py-3' : 'border-b px-4 py-3'

  return (
    <tr>
      <td className={`${cell} tabular-nums`}>{data}</td>
      <td className={cell}>{tipo}</td>
      <td className={cell}>
        <span
          className={`iu-label inline-flex h-6 items-center rounded-sm px-2 whitespace-nowrap ${tons[tom]}`}
        >
          {status}
        </span>
      </td>
    </tr>
  )
}

import type { Metadata } from 'next'
import { env } from '@/config/env'
import { LandingHeader } from '@/features/landing/LandingHeader'
import {
  Faq,
  FinalCta,
  Features,
  Footer,
  Hero,
  HowItWorks,
  Pricing,
  Principle,
  Roles,
  Security,
} from '@/features/landing/sections'

const title = `${env.appName} — gestão para escritórios de advocacia`
const description =
  'Monitoramento do DJEN/CNJ, sugestão de prazos por IA e revisão humana obrigatória. Nenhum prazo escapa; você continua decidindo.'

export const metadata: Metadata = {
  title: { absolute: title },
  description,
  // Sobrescreve o noindex do layout raiz: a landing é a única página pública indexável.
  robots: { index: true, follow: true },
  openGraph: { title, description, type: 'website', locale: 'pt_BR', siteName: env.appName },
  twitter: { card: 'summary', title, description },
}

const jsonLd = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: env.appName,
  applicationCategory: 'BusinessApplication',
  operatingSystem: 'Web',
  description,
  inLanguage: 'pt-BR',
}

export default function LandingPage() {
  return (
    <>
      <a
        href="#conteudo"
        className="sr-only z-50 rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        Pular para o conteúdo
      </a>
      <LandingHeader />
      <main id="conteudo" tabIndex={-1} className="outline-none">
        <Hero />
        <HowItWorks />
        <Principle />
        <Features />
        <Roles />
        <Security />
        <Pricing />
        <Faq />
        <FinalCta />
      </main>
      <Footer />
      <script
        type="application/ld+json"
        // `<` escapado: JSON.stringify não sanitiza (guia de JSON-LD do Next.js).
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
      />
    </>
  )
}

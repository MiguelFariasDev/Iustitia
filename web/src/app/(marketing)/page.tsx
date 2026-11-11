import type { Metadata } from 'next'
import { env } from '@/config/env'
import { HeroSection } from '@/features/landing/components/HeroSection'
import {
  AudienceSection,
  FaqSection,
  FeaturesSection,
  FinalCtaSection,
  HowItWorksSection,
  ResponsibleAiSection,
  SecuritySection,
} from '@/features/landing/components/Sections'

export const metadata: Metadata = {
  title: { absolute: `${env.appName} · Gestão para escritórios de advocacia` },
  description:
    'Prazos, publicações, processos, agenda e atendimento num só lugar, com inteligência artificial como apoio e revisão humana em cada etapa.',
}

/** Visitantes veem a landing; quem já tem sessão é levado ao painel pelo proxy (src/proxy.ts). */
export default function LandingPage() {
  return (
    <>
      <HeroSection />
      <HowItWorksSection />
      <FeaturesSection />
      <ResponsibleAiSection />
      <SecuritySection />
      <AudienceSection />
      <FaqSection />
      <FinalCtaSection />
    </>
  )
}

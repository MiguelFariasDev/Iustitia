import { LandingFooter } from '@/features/landing/components/LandingFooter'
import { LandingHeader } from '@/features/landing/components/LandingHeader'

export default function MarketingLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <LandingHeader />
      <main id="conteudo" tabIndex={-1} className="outline-none">
        {children}
      </main>
      <LandingFooter />
    </>
  )
}

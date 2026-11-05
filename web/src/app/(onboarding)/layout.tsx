import { IuScope } from '@/components/iu/IuScope'
import { geist } from '@/lib/fonts'

export default function OnboardingLayout({ children }: { children: React.ReactNode }) {
  return (
    <IuScope fontClass={geist.variable}>
      <main id="conteudo" tabIndex={-1} className="outline-none">
        {children}
      </main>
    </IuScope>
  )
}

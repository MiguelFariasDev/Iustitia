import { IuScope } from '@/components/iu/IuScope'
import { geist } from '@/lib/fonts'

export default function MarketingLayout({ children }: { children: React.ReactNode }) {
  return <IuScope fontClass={geist.variable}>{children}</IuScope>
}

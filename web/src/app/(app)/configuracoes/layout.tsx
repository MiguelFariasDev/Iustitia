import { PageHeader } from '@/components/shared/PageHeader'
import { SettingsNav } from '@/features/settings/components/SettingsNav'

export default function SettingsLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <PageHeader title="Configurações" description="Perfil, escritório, equipe e integrações." />
      <div className="grid gap-6 lg:grid-cols-[13rem_minmax(0,1fr)]">
        <SettingsNav />
        <div className="min-w-0">{children}</div>
      </div>
    </>
  )
}

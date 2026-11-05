import type { Metadata } from 'next'
import { LegalPage } from '@/features/landing/LegalPage'

export const metadata: Metadata = { title: 'Política de privacidade' }

export default function PrivacyPage() {
  return (
    <LegalPage
      title="Política de privacidade"
      sections={[
        { heading: 'Controlador e encarregado (DPO)', hint: 'identificação e canal de contato.' },
        { heading: 'Dados tratados e finalidades', hint: 'quais dados, para quê e sob qual base legal (LGPD).' },
        { heading: 'Compartilhamento e uso de IA', hint: 'operadores envolvidos; dados processuais não treinam modelos de terceiros.' },
        { heading: 'Segurança e retenção', hint: 'criptografia, auditoria e prazos de guarda.' },
        { heading: 'Direitos do titular', hint: 'acesso, correção, exportação e exclusão, e como exercê-los.' },
      ]}
    />
  )
}

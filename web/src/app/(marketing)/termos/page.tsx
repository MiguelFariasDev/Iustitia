import type { Metadata } from 'next'
import { LegalPage } from '@/features/landing/LegalPage'

export const metadata: Metadata = { title: 'Termos de uso' }

export default function TermsPage() {
  return (
    <LegalPage
      title="Termos de uso"
      sections={[
        { heading: 'Objeto e aceite', hint: 'o que é o serviço e quem pode contratá-lo.' },
        { heading: 'Uso de inteligência artificial', hint: 'a IA sugere; a decisão e a responsabilidade finais são do advogado (Provimento CFOAB nº 271/2025).' },
        { heading: 'Responsabilidades do escritório', hint: 'veracidade dos dados e guarda das credenciais.' },
        { heading: 'Disponibilidade e limites', hint: 'níveis de serviço e fontes de dados (DJEN/CNJ).' },
        { heading: 'Encerramento', hint: 'cancelamento, exportação e exclusão de dados.' },
      ]}
    />
  )
}

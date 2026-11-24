import { routes } from '@/config/routes'
import { SemDadosPanel } from './SemDadosPanel'

/**
 * Os lançamentos do financeiro ainda ficam só no navegador de quem os fez, então o painel não
 * consolida valores do escritório. A tela Financeiro mostra o que você lançou.
 */
export function FinanceiroPanel() {
  return (
    <SemDadosPanel
      titulo="Valores do escritório ainda indisponíveis"
      descricao="O financeiro ainda não tem servidor, então o painel não consolida os valores do escritório. Os lançamentos que você fizer aparecem na tela Financeiro."
      acao={{ href: routes.finance, label: 'Abrir o financeiro' }}
    />
  )
}

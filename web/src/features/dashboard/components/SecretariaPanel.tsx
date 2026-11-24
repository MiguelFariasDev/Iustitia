import { routes } from '@/config/routes'
import { SemDadosPanel } from './SemDadosPanel'

/**
 * A API só admite advogado e sócio em publicações e processos (ADR-068), então o apoio
 * administrativo não tem ainda de onde tirar dado real. A tela diz isso em vez de simular.
 */
export function SecretariaPanel() {
  return (
    <SemDadosPanel
      titulo="Nada para mostrar por enquanto"
      descricao="Os indicadores de apoio administrativo aparecem aqui quando os módulos correspondentes estiverem disponíveis. Enquanto isso, a agenda e os documentos funcionam normalmente."
      acao={{ href: routes.agenda, label: 'Abrir a agenda' }}
    />
  )
}

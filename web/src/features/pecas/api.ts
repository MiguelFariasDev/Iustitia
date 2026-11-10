import { mockResponse } from '@/lib/api/mock'
import { montarMinuta, type DadosProcesso, type Minuta, type TipoPeca } from './modelos'

/**
 * Geração da minuta. SÓ EM DEMONSTRAÇÃO: não existe API de geração, e a tela não chama isto sem
 * mocks (ver PecasView). A latência simulada exercita o estado de carregamento como num serviço
 * de IA de verdade, que leva segundos.
 */
export function gerarMinuta(tipo: TipoPeca, processo: DadosProcesso): Promise<Minuta> {
  return mockResponse(montarMinuta(tipo, processo), 1300)
}

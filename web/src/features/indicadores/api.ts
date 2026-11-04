import { mockResponse } from '@/lib/api/mock'
import { mockIndicadores } from './mocks'
import type { Indicadores, Periodo } from './types'

/**
 * Não há endpoint de relatórios: sempre mock (ver mocks.ts).
 *
 * TODO(indicadores-backend): `GET /relatorios/indicadores?periodo=` — agregado no servidor.
 * Compor no cliente a partir de `/processos` e `/publications` cobriria só parte dos números
 * (prazos e tarefas não existem) e misturaria dado real com inventado na mesma tela.
 */
export function getIndicadores(periodo: Periodo): Promise<Indicadores> {
  return mockResponse(mockIndicadores(periodo), 300)
}

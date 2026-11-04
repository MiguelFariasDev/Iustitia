'use client'

import { useQuery } from '@tanstack/react-query'
import { indicadoresQuery } from './queries'
import type { Periodo } from './types'

/**
 * Dados agregados do período. `placeholderData` mantém os gráficos anteriores na tela
 * enquanto o novo período carrega, em vez de piscar o skeleton a cada clique no filtro.
 */
export function useIndicadores(periodo: Periodo) {
  return useQuery({ ...indicadoresQuery(periodo), placeholderData: (previous) => previous })
}

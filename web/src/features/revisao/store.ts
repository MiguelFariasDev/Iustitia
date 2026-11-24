import { create } from 'zustand'
import type { Resultado } from './fila'

interface RevisaoState {
  /** Decisões desta sessão, por id de publicação. */
  decisoes: Record<string, Resultado>
  registrar: (id: string, resultado: Resultado) => void
  limpar: () => void
}

/**
 * Só a memória do que foi decidido NESTA sessão, para a tela avançar e mostrar o resultado no lugar
 * dos botões. A decisão de verdade é gravada na API (status da publicação + evento na agenda); ao
 * recarregar, a publicação decidida já não volta como pendente porque o servidor a conhece.
 */
export const useRevisaoStore = create<RevisaoState>((set) => ({
  decisoes: {},
  registrar: (id, resultado) => set((s) => ({ decisoes: { ...s.decisoes, [id]: resultado } })),
  limpar: () => set({ decisoes: {} }),
}))

import { useSyncExternalStore } from 'react'
import { useAuthStore } from '@/stores/authStore'

const reinicios: Array<() => void> = []

/** Só para testes: esquece o que está em memória (o localStorage é limpo à parte). */
export function reiniciarArmazens() {
  reinicios.forEach((reiniciar) => reiniciar())
}

/**
 * Armazém local por usuário, para módulos que ainda não têm servidor. Os dados ficam NESTE
 * NAVEGADOR (localStorage), separados por usuário: não sincronizam entre aparelhos nem entre
 * pessoas do escritório. Cada tela que usa isto precisa dizer isso ao usuário.
 */
export function criarArmazem<T>(prefixo: string, vazio: T) {
  let cache: { chave: string; valor: T } | null = null
  const ouvintes = new Set<() => void>()

  const chave = () => `${prefixo}${useAuthStore.getState().user?.id ?? 'anonimo'}`

  function ler(): T {
    const atual = chave()
    if (cache?.chave === atual) return cache.valor

    let valor = vazio
    try {
      const bruto = window.localStorage.getItem(atual)
      if (bruto) valor = { ...vazio, ...(JSON.parse(bruto) as T) }
    } catch {
      // Armazenamento bloqueado ou conteúdo corrompido: começa vazio.
    }
    cache = { chave: atual, valor }

    return valor
  }

  function gravar(valor: T) {
    const atual = chave()
    cache = { chave: atual, valor }
    try {
      window.localStorage.setItem(atual, JSON.stringify(valor))
    } catch {
      // Cheio ou bloqueado (janela privada): vale só até recarregar a página.
    }
    ouvintes.forEach((ouvinte) => ouvinte())
  }

  function assinar(ouvinte: () => void) {
    ouvintes.add(ouvinte)

    return () => {
      ouvintes.delete(ouvinte)
    }
  }

  reinicios.push(() => {
    cache = null
  })

  function useArmazem(): T {
    return useSyncExternalStore(assinar, ler, () => vazio)
  }

  return { ler, gravar, assinar, useArmazem }
}

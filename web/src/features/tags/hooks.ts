import { useQuery, useQueryClient } from '@tanstack/react-query'
import { notify } from '@/lib/notify'
import { criarTagApi, definirTagApi, listarTags, removerTagApi } from './api'
import type { AlvoTag, EstadoTags, Tag } from './types'

const CHAVE = ['tags'] as const
const VAZIO: EstadoTags = { tags: [], atribuicoes: {} }

export function useTagsState(): EstadoTags {
  const { data } = useQuery({ queryKey: CHAVE, queryFn: listarTags, staleTime: 60_000 })

  return data ?? VAZIO
}

/** Ações sobre tags. Marcar/desmarcar atualiza a tela na hora e desfaz se o servidor recusar. */
export function useTagActions() {
  const queryClient = useQueryClient()
  const recarregar = () => queryClient.invalidateQueries({ queryKey: CHAVE })
  const falhou = (mensagem: string) => {
    notify.error(mensagem)
    void recarregar()
  }

  async function criar(nome: string): Promise<Tag | null> {
    try {
      const tag = await criarTagApi(nome)
      await recarregar()

      return tag
    } catch {
      falhou('Não foi possível criar a tag.')

      return null
    }
  }

  async function definir(alvo: AlvoTag, alvoId: string, tagId: string, aplicada: boolean) {
    const chave = `${alvo}:${alvoId}`
    queryClient.setQueryData<EstadoTags>(CHAVE, (atual) => {
      const estado = atual ?? VAZIO
      const ids = (estado.atribuicoes[chave] ?? []).filter((id) => id !== tagId)

      return {
        ...estado,
        atribuicoes: { ...estado.atribuicoes, [chave]: aplicada ? [...ids, tagId] : ids },
      }
    })
    try {
      await definirTagApi(alvo, alvoId, tagId, aplicada)
    } catch {
      falhou('Não foi possível atualizar as tags.')
    }
  }

  async function remover(tagId: string) {
    try {
      await removerTagApi(tagId)
      await recarregar()
    } catch {
      falhou('Não foi possível apagar a tag.')
    }
  }

  return { criar, definir, remover }
}

import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { alternarTag, criarTag, lerEstadoLocal, removerTag } from './repositorio'
import type { AlvoTag, EstadoTags, Tag } from './types'

interface TagsDto {
  items: { id: string; name: string; color: string }[]
  assignments: { tagId: string; target: string; targetId: string }[]
}

const alvoApi: Record<AlvoTag, string> = { processo: 'Processo', advogado: 'Advogado' }
const alvoLocal: Record<string, AlvoTag> = { Processo: 'processo', Advogado: 'advogado' }

export async function listarTags(): Promise<EstadoTags> {
  if (env.useMocks) return lerEstadoLocal()

  const { data } = await apiClient.get<TagsDto>(endpoints.tags.root)
  const atribuicoes: Record<string, string[]> = {}
  for (const a of data.assignments) {
    const alvo = alvoLocal[a.target]
    if (!alvo) continue
    const chave = `${alvo}:${a.targetId}`
    atribuicoes[chave] = [...(atribuicoes[chave] ?? []), a.tagId]
  }

  return {
    tags: data.items.map((t) => ({ id: t.id, nome: t.name, cor: t.color })),
    atribuicoes,
  }
}

/** Cria a tag; se o nome já existe, o servidor devolve a existente. */
export async function criarTagApi(nome: string): Promise<Tag | null> {
  if (env.useMocks) return criarTag(nome)
  if (!nome.trim()) return null

  const { data } = await apiClient.post<{ id: string; name: string; color: string }>(
    endpoints.tags.root,
    { name: nome.trim() }
  )

  return { id: data.id, nome: data.name, cor: data.color }
}

export async function definirTagApi(
  alvo: AlvoTag,
  alvoId: string,
  tagId: string,
  aplicada: boolean
): Promise<void> {
  if (env.useMocks) {
    const atual = lerEstadoLocal().atribuicoes[`${alvo}:${alvoId}`] ?? []
    if (atual.includes(tagId) !== aplicada) alternarTag(alvo, alvoId, tagId)

    return
  }
  await apiClient.put(endpoints.tags.atribuicoes, {
    tagId,
    target: alvoApi[alvo],
    targetId: alvoId,
    applied: aplicada,
  })
}

export async function removerTagApi(tagId: string): Promise<void> {
  if (env.useMocks) {
    removerTag(tagId)

    return
  }
  await apiClient.delete(endpoints.tags.byId(tagId))
}

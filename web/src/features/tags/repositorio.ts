import { criarArmazem } from '@/lib/storage/armazem'
import { coresTag, type AlvoTag, type EstadoTags, type Tag } from './types'

/**
 * Tags guardadas neste navegador, por usuário (ver armazem.ts). Só vale no modo de mocks:
 * com servidor, as tags vêm da API (api.ts).
 */
const armazem = criarArmazem<EstadoTags>('iustitia:tags:v1:', { tags: [], atribuicoes: {} })

/** Implementação local (modo de mocks). Com servidor, as telas usam api.ts + hooks.ts. */
export const lerEstadoLocal = armazem.ler

const chaveDe = (alvo: AlvoTag, id: string) => `${alvo}:${id}`

export function tagsDe(estado: EstadoTags, alvo: AlvoTag, id: string): Tag[] {
  const ids = estado.atribuicoes[chaveDe(alvo, id)] ?? []

  return estado.tags.filter((tag) => ids.includes(tag.id))
}

/** Cria a tag; se já existe uma com o mesmo nome (sem diferenciar maiúsculas), reaproveita. */
export function criarTag(nome: string, cor?: string): Tag | null {
  const limpo = nome.trim().slice(0, 30)
  if (!limpo) return null

  const estado = armazem.ler()
  const existente = estado.tags.find((t) => t.nome.toLowerCase() === limpo.toLowerCase())
  if (existente) return existente

  const tag: Tag = {
    id: crypto.randomUUID(),
    nome: limpo,
    cor: cor ?? coresTag[estado.tags.length % coresTag.length]!,
  }
  armazem.gravar({ ...estado, tags: [...estado.tags, tag] })

  return tag
}

export function alternarTag(alvo: AlvoTag, id: string, tagId: string) {
  const estado = armazem.ler()
  const chave = chaveDe(alvo, id)
  const atuais = estado.atribuicoes[chave] ?? []
  const proximas = atuais.includes(tagId) ? atuais.filter((t) => t !== tagId) : [...atuais, tagId]

  armazem.gravar({ ...estado, atribuicoes: { ...estado.atribuicoes, [chave]: proximas } })
}

/** Apaga a tag do catálogo e de todo lugar onde ela estava aplicada. */
export function removerTag(tagId: string) {
  const estado = armazem.ler()
  const atribuicoes = Object.fromEntries(
    Object.entries(estado.atribuicoes).map(([chave, ids]) => [chave, ids.filter((t) => t !== tagId)])
  )
  armazem.gravar({ tags: estado.tags.filter((t) => t.id !== tagId), atribuicoes })
}

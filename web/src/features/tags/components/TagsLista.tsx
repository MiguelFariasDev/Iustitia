'use client'

import { useTagsState } from '../hooks'
import { tagsDe } from '../repositorio'
import type { AlvoTag } from '../types'
import { TagBadge } from './TagBadge'

/** Só exibe as tags aplicadas (listagens); para editar, use TagsEditor. */
export function TagsLista({ alvo, alvoId }: { alvo: AlvoTag; alvoId: string }) {
  const tags = tagsDe(useTagsState(), alvo, alvoId)
  if (tags.length === 0) return null

  return (
    <div className="mt-1 flex flex-wrap gap-1">
      {tags.map((tag) => (
        <TagBadge key={tag.id} tag={tag} />
      ))}
    </div>
  )
}

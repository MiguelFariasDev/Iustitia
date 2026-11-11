import type { Tag } from '../types'

export function TagBadge({ tag }: { tag: Tag }) {
  return (
    <span
      className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium"
      style={{ color: tag.cor, borderColor: `${tag.cor}66`, backgroundColor: `${tag.cor}1a` }}
    >
      {tag.nome}
    </span>
  )
}

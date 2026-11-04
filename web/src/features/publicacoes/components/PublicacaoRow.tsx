import { FileSearch } from 'lucide-react'
import Link from 'next/link'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'
import type { Publication } from '../types'

/**
 * Conteúdo da célula "Processo": número, trecho do texto e o marcador de não lida. O trecho
 * existe porque na rotina do escritório a decisão "preciso abrir isto agora?" costuma sair
 * das primeiras linhas — o clique vai para o detalhe, com a publicação inteira.
 */
export function PublicacaoRowSummary({ item }: { item: Publication }) {
  return (
    <div className="min-w-0 space-y-1">
      <div className="flex items-center gap-2">
        {!item.isRead ? (
          <span
            className="size-2 shrink-0 rounded-full bg-secondary"
            role="img"
            aria-label="Não lida"
          />
        ) : null}
        {item.processNumber ? (
          <Link
            href={`${routes.publications}/${item.id}` as never}
            className={cn(
              'rounded font-mono text-xs whitespace-nowrap underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
              !item.isRead && 'font-semibold'
            )}
          >
            {item.processNumber}
          </Link>
        ) : (
          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
            <FileSearch className="size-3.5" aria-hidden />
            Processo não encontrado
          </span>
        )}
      </div>
      <p className="line-clamp-2 max-w-xl text-sm text-muted-foreground">{item.content}</p>
    </div>
  )
}

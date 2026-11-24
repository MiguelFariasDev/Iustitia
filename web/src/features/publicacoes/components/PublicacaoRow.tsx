'use client'

import { FileSearch } from 'lucide-react'
import Link from 'next/link'
import { routes } from '@/config/routes'
import { formatRelative } from '@/lib/format'
import { notify } from '@/lib/notify'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'
import { useTenantStore } from '@/stores/tenantStore'
import type { Publication } from '../types'
import { canReviewAi, statusBadgeOf } from '../view'

/**
 * Conteúdo da célula "Processo": número, trecho do texto, tempo desde a divulgação e o marcador
 * de não lida. O trecho existe porque na rotina do escritório a decisão "preciso abrir isto
 * agora?" costuma sair das primeiras linhas.
 *
 * O bloco INTEIRO é o link para a publicação — inclusive quando ela não tem processo (antes só o
 * número era link, e uma publicação sem processo não tinha como ser aberta).
 *
 * Revisar o que a IA sugeriu é ato de advogado ou sócio: quem não é, ao clicar numa publicação
 * em revisão da IA, recebe o aviso e não navega.
 */
export function PublicacaoRowSummary({ item }: { item: Publication }) {
  const role = useAuthStore((s) => s.user?.role)
  const aiEnabled = useTenantStore((s) => s.featureFlags['ai.classification'] === true)
  const emRevisaoDaIa = statusBadgeOf(item, aiEnabled).ai

  return (
    <Link
      href={`${routes.publications}/${item.id}` as never}
      onClick={(event) => {
        if (emRevisaoDaIa && !canReviewAi(role)) {
          event.preventDefault()
          notify.info('Revisão da IA é feita por advogado ou sócio')
        }
      }}
      className="block min-w-0 space-y-1 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="flex items-center gap-2">
        {!item.isRead ? (
          <span className="size-2 shrink-0 rounded-full bg-ia" role="img" aria-label="Não lida" />
        ) : null}
        {item.processNumber ? (
          <span
            className={cn('font-mono text-xs whitespace-nowrap', !item.isRead && 'font-semibold')}
          >
            {item.processNumber}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
            <FileSearch className="size-3.5" aria-hidden />
            Processo não encontrado
          </span>
        )}
        <time
          dateTime={item.publishedAt}
          className="ml-auto text-xs whitespace-nowrap text-muted-foreground"
        >
          {formatRelative(item.publishedAt)}
        </time>
      </span>
      <span
        className={cn(
          'line-clamp-2 block max-w-sm text-sm',
          item.isRead ? 'text-muted-foreground' : 'font-semibold text-foreground'
        )}
      >
        {item.content}
      </span>
    </Link>
  )
}

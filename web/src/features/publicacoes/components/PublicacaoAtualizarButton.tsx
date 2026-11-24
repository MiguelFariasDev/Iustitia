'use client'

import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { cn } from '@/lib/utils'
import { publicationsSummaryQuery } from '../queries'
import type { PublicationsSummary } from '../types'

const hora = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' })

/**
 * Refaz as consultas de publicações e alertas e CONTA o que mudou: a captura em si é do job (a
 * cada poucas horas), então o clique quase sempre não traz nada, e um botão que gira e não diz
 * nada parece quebrado. A comparação é do total antes e depois, e o "desde" é a hora em que a
 * contagem anterior foi carregada — não um horário fixo.
 */
export function PublicacaoAtualizarButton() {
  const queryClient = useQueryClient()
  const fetching = useIsFetching({ queryKey: queryKeys.publications.all }) > 0

  async function atualizar() {
    const key = publicationsSummaryQuery().queryKey
    const antes = queryClient.getQueryData<PublicationsSummary>(key)
    const desde = queryClient.getQueryState(key)?.dataUpdatedAt

    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.publications.all }),
      queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all }),
    ])

    const depois = queryClient.getQueryData<PublicationsSummary>(key)
    if (!antes || !depois) return

    const novas = depois.total - antes.total
    if (novas > 0) {
      notify.success(novas === 1 ? '1 publicação nova' : `${novas} publicações novas`)
    } else {
      notify.info(
        desde
          ? `Nenhuma publicação nova desde ${hora.format(new Date(desde))}`
          : 'Nenhuma publicação nova'
      )
    }
  }

  return (
    <Button variant="outline" size="sm" disabled={fetching} onClick={() => void atualizar()}>
      <RefreshCw className={cn(fetching && 'animate-spin')} aria-hidden />
      {fetching ? 'Atualizando…' : 'Atualizar'}
    </Button>
  )
}

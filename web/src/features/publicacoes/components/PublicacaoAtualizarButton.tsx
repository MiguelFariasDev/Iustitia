'use client'

import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { queryKeys } from '@/lib/query/queryKeys'
import { cn } from '@/lib/utils'

/** Refaz as consultas de publicações e alertas (a captura em si é do job, a cada poucas horas). */
export function PublicacaoAtualizarButton() {
  const queryClient = useQueryClient()
  const fetching = useIsFetching({ queryKey: queryKeys.publications.all }) > 0

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={fetching}
      onClick={() => {
        void queryClient.invalidateQueries({ queryKey: queryKeys.publications.all })
        void queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all })
      }}
    >
      <RefreshCw className={cn(fetching && 'animate-spin')} aria-hidden />
      {fetching ? 'Atualizando…' : 'Atualizar'}
    </Button>
  )
}

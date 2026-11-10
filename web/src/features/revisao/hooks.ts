'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { createEvento } from '@/features/agenda/api'
import { toDateTimeLocal } from '@/features/agenda/dates'
import { eventoFormDefaults } from '@/features/agenda/schemas'
import { changePublicationStatus } from '@/features/publicacoes/api'
import { publicationsQuery } from '@/features/publicacoes/queries'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { addBusinessDays } from './diasUteis'
import { montarFila, rotuloDecisao, type Decisao, type ItemRevisao } from './fila'
import { useRevisaoStore } from './store'

/** Publicações aguardando revisão, com a sugestão de demonstração. Só roda com `enabled`. */
export function useFilaRevisao(enabled: boolean) {
  const query = useQuery({
    ...publicationsQuery({ status: 'untreated', pageSize: 100 }),
    enabled,
  })
  const itens = useMemo(() => montarFila(query.data?.items ?? []), [query.data])

  return {
    itens,
    isPending: enabled && query.isPending,
    isError: query.isError,
    refetch: query.refetch,
  }
}

/** yyyy-MM-dd local de uma data. */
const isoDia = (data: Date) => toDateTimeLocal(data).slice(0, 10)

interface DecidirInput {
  item: ItemRevisao
  decisao: Decisao
  /** Prazo final em dias úteis (o sugerido ao aceitar, o digitado ao corrigir). */
  dias: number
}

/**
 * Registra a decisão do advogado. Aceitar e corrigir salvam o prazo na agenda E mudam o status da
 * publicação (Approved / Corrected, com a nota do que foi sugerido); rejeitar só muda o status
 * (Rejected). Nada é salvo sem esta chamada — o clique é a decisão (ADR-012).
 */
export function useDecidirRevisao(onDecidido?: (id: string) => void) {
  const queryClient = useQueryClient()
  const registrar = useRevisaoStore((s) => s.registrar)

  return useMutation({
    mutationFn: async ({ item, decisao, dias }: DecidirInput) => {
      const { publication, sugestao } = item

      if (decisao === 'rejected') {
        await changePublicationStatus(
          publication.id,
          'Rejected',
          `Sugestão da IA rejeitada (sugeria ${sugestao.dias} dias úteis).`
        )
        return { vencimento: null as string | null }
      }

      const vencimento = addBusinessDays(new Date(publication.publishedAt), dias)
      const numero = publication.processNumber ? ` — ${publication.processNumber}` : ''

      await createEvento({
        ...eventoFormDefaults(`${isoDia(vencimento)}T00:00`, {
          tipo: 'Prazo',
          diaInteiro: true,
          titulo: `Prazo: ${sugestao.tipo}${numero}`,
          processoId: publication.processId ?? undefined,
          // Só a referência: o inteiro teor da publicação não vai para a descrição do evento.
          descricao: `${dias} dias úteis a partir da publicação de ${isoDia(new Date(publication.publishedAt))}. Estimativa sem feriados: confira antes de contar com a data.`,
        }),
      })

      await changePublicationStatus(
        publication.id,
        decisao === 'corrected' ? 'Corrected' : 'Approved',
        decisao === 'corrected'
          ? `Prazo corrigido de ${sugestao.dias} para ${dias} dias úteis.`
          : `Prazo de ${dias} dias úteis aceito conforme sugerido.`
      )

      return { vencimento: isoDia(vencimento) }
    },
    onSuccess: ({ vencimento }, { item, decisao, dias }) => {
      registrar(item.publication.id, {
        decisao,
        dias: decisao === 'rejected' ? null : dias,
        vencimento,
        sugeridoDias: item.sugestao.dias,
      })
      notify.success(rotuloDecisao[decisao])
      void queryClient.invalidateQueries({ queryKey: queryKeys.publications.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agenda.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.navCounts })
      onDecidido?.(item.publication.id)
    },
    onError: (error) => handleApiError(error),
  })
}

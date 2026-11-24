'use client'

import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { useAgenda } from '@/features/agenda/hooks'
import { processosQuery } from '@/features/processos/queries'
import { publicationsQuery } from '@/features/publicacoes/queries'
import { useAuthStore } from '@/stores/authStore'
import { addDays, startOfDay } from './time'
import { QUEUE_WINDOW_DAYS, type ReviewPublication } from './workQueue'

/** Quantas publicações a revisar a fila mostra. */
const REVIEW_ITEMS = 5

/**
 * O que o painel sabe de verdade sobre o trabalho a fazer: prazos e compromissos da agenda (hoje
 * até 7 dias à frente) e as publicações que a IA leu e esperam revisão.
 *
 * `scope`: "mine" filtra a agenda pelo usuário logado (visão do advogado); "all" traz a do
 * escritório (visão do sócio). `now` nulo = ainda hidratando: nada é buscado.
 */
export function useDashboardWork(
  now: Date | null,
  scope: 'mine' | 'all',
  /**
   * `withQueue`: também busca processos (para o número na linha) e publicações a revisar. Só o
   * painel do advogado monta a fila; o do sócio só precisa dos prazos da agenda.
   */
  { withQueue = true }: { withQueue?: boolean } = {}
) {
  const userId = useAuthStore((s) => s.user?.id ?? null)
  const dia = now ? startOfDay(now).getTime() : null

  const periodo = useMemo(() => {
    if (dia === null) return { from: '', to: '' }
    const inicio = new Date(dia)

    return {
      from: inicio.toISOString(),
      to: addDays(inicio, QUEUE_WINDOW_DAYS + 1).toISOString(),
    }
  }, [dia])

  const agenda = useAgenda(periodo, scope === 'mine' ? userId : null, { enabled: dia !== null })
  const eventos = useMemo(() => agenda.data ?? [], [agenda.data])

  // Só busca os processos se algum evento está vinculado a um: é o que dá o número na linha.
  const precisaProcessos = withQueue && eventos.some((evento) => evento.processoId !== null)
  const processos = useQuery({
    ...processosQuery({ pageSize: 100, status: 'Ativo' }),
    enabled: precisaProcessos,
  })
  const processNumbers = useMemo(
    () => new Map((processos.data?.items ?? []).map((p) => [p.id, p.cnjNumber])),
    [processos.data]
  )

  const revisoes = useQuery({
    ...publicationsQuery({ status: 'untreated', pageSize: REVIEW_ITEMS }),
    enabled: withQueue && dia !== null,
  })
  const reviews = useMemo<ReviewPublication[]>(
    () =>
      (revisoes.data?.items ?? []).map((publication) => ({
        id: publication.id,
        processNumber: publication.processNumber,
        source: publication.tribunal ?? publication.journal,
      })),
    [revisoes.data]
  )

  return {
    eventos,
    processNumbers,
    reviews,
    isPending: dia === null || agenda.isPending,
    isError: agenda.isError,
    refetch: () => void agenda.refetch(),
  }
}

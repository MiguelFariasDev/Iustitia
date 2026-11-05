'use client'

import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { env, isMockAuth } from '@/config/env'
import { getSupabaseBrowserClient, isSupabaseConfigured } from '@/lib/supabase/client'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { useAuthStore } from '@/stores/authStore'
import { parseRealtimePayload } from './contract'
import { mockClientMessage, mockFindOpenByPhone } from './mocks'

/** Mesmos nomes que o backend usa (BuildingBlocks.Application.Abstractions.RealtimeChannels). */
export const ATENDIMENTO_UPDATED_EVENT = 'atendimento.updated'
export const ATENDIMENTO_MESSAGE_EVENT = 'atendimento.message.created'

/**
 * Evento de janela que SIMULA o cliente escrevendo (e o Bot reagindo) quando a aplicação roda com
 * a API simulada (NEXT_PUBLIC_API_MOCKS=true — dev e E2E, nunca produção). É o equivalente local
 * do broadcast: depois de alterar o "banco" em memória, passa pelo MESMO tratamento do canal real.
 * `detail`: `{ phoneNumber, text }` ou `{ atendimentoId, text }`.
 */
export const MOCK_ATENDIMENTO_EVENT = 'adv:atendimento-client-message'

export function atendimentosChannelName(tenantId: string) {
  return `tenant:${tenantId}:atendimentos`
}

/**
 * Tempo real da tela de Atendimentos: assina o canal PRIVADO do escritório no Supabase Realtime
 * e, a cada aviso, invalida o cache — a fila e a conversa aberta se atualizam sozinhas.
 *
 * O canal só carrega ids e estado (nunca telefone nem texto); por isso a única coisa que se faz
 * com o aviso é recarregar pela API autenticada. A policy de `realtime.messages`
 * (infra/supabase/migrations/014_atendimentos.sql) só deixa assinar Owner/Partner/Lawyer do
 * próprio tenant, e o token precisa ir ao Realtime (`setAuth`) ANTES da assinatura.
 *
 * Quando um cliente passa a aguardar um humano, avisa com um toast — é o evento que pede ação.
 *
 * Sem Supabase configurado, em modo mock de autenticação ou sem sessão, não assina nada, e o
 * polling das queries cobre. Falha de conexão nunca derruba a tela: realtime é conveniência.
 */
export function useAtendimentosRealtime() {
  const queryClient = useQueryClient()
  const tenantId = useAuthStore((s) => s.user?.tenantId)
  const userId = useAuthStore((s) => s.user?.id)
  const role = useAuthStore((s) => s.user?.role)

  // As conversas de clientes são sigilosas: a policy do canal e a API só admitem Owner/Partner/Lawyer
  // (LawyerOrAbove). Quem não pode lê-las nem tenta assinar.
  const canReadConversations = role === 'Owner' || role === 'Partner' || role === 'Lawyer'

  useEffect(() => {
    if (!tenantId || !canReadConversations) return

    /** Caminho único para um aviso recebido, venha ele do canal real ou do simulado. */
    function onNotice(payload: unknown) {
      const notice = parseRealtimePayload(payload)
      if (!notice) return false

      void queryClient.invalidateQueries({ queryKey: queryKeys.atendimentos.all })

      if (notice.status === 'PendingHuman') {
        notify.info('Um cliente está aguardando atendimento.')
      } else if (
        notice.status === 'Human' &&
        notice.assignedUserId &&
        notice.assignedUserId !== userId
      ) {
        notify.info('Outro advogado assumiu um atendimento.')
      }

      return true
    }

    if (env.useMocks) {
      const listener = (event: Event) => {
        const detail = (
          event as CustomEvent<{ phoneNumber?: string; atendimentoId?: string; text: string }>
        ).detail
        const id =
          detail.atendimentoId ??
          (detail.phoneNumber ? mockFindOpenByPhone(detail.phoneNumber) : null)
        if (!id) return

        // Em produção o dado já está no banco quando o aviso chega; aqui o "banco" é o store em memória.
        try {
          for (const notice of mockClientMessage(id, detail.text).events) onNotice(notice)
        } catch {
          // Conversa inexistente no simulador: um evento de dev/teste perdido não derruba a tela.
        }
      }
      window.addEventListener(MOCK_ATENDIMENTO_EVENT, listener)
      return () => window.removeEventListener(MOCK_ATENDIMENTO_EVENT, listener)
    }

    if (isMockAuth || !isSupabaseConfigured()) return

    let cancelled = false
    let cleanup: (() => void) | undefined

    void (async () => {
      try {
        const supabase = getSupabaseBrowserClient()
        await supabase.realtime.setAuth()
        if (cancelled) return

        const channel = supabase
          .channel(atendimentosChannelName(tenantId), { config: { private: true } })
          .on('broadcast', { event: ATENDIMENTO_UPDATED_EVENT }, ({ payload }) => onNotice(payload))
          .on('broadcast', { event: ATENDIMENTO_MESSAGE_EVENT }, ({ payload }) => onNotice(payload))
          .subscribe()

        cleanup = () => void supabase.removeChannel(channel)
        if (cancelled) cleanup()
      } catch {
        // Realtime é conveniência: o polling garante a convergência.
      }
    })()

    return () => {
      cancelled = true
      cleanup?.()
    }
  }, [queryClient, tenantId, userId, canReadConversations])
}

'use client'

import { useQueryClient } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { env, isMockAuth } from '@/config/env'
import { routes } from '@/config/routes'
import { getSupabaseBrowserClient, isSupabaseConfigured } from '@/lib/supabase/client'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { useAuthStore } from '@/stores/authStore'
import { parseRealtimePayload } from './contract'
import { mockPushAlerta } from './mocks'

/** Mesmos nomes que o backend usa (Platform.Application.Abstractions.RealtimeChannels). */
export const ALERT_CREATED_EVENT = 'alert.created'

/**
 * Evento de janela que SIMULA a chegada de um alerta quando a aplicação roda com a API
 * simulada (NEXT_PUBLIC_API_MOCKS=true — dev e E2E, nunca produção). É o equivalente local do
 * broadcast do Supabase: passa pelo mesmo tratamento (validação, toast, invalidação de cache).
 */
export const MOCK_ALERT_EVENT = 'adv:alert-created'

export function alertasChannelName(tenantId: string, userId: string) {
  return `tenant:${tenantId}:alerts:${userId}`
}

/**
 * Notificação in-app em tempo real: assina o canal PRIVADO do usuário no Supabase Realtime e,
 * a cada alerta novo, mostra o toast e invalida o cache — o sino e a lista se atualizam sozinhos.
 *
 * O canal é privado (`private: true`): o Supabase só deixa assinar se a policy de
 * `realtime.messages` (infra/supabase/migrations/013_alerts.sql) permitir, e ela compara o
 * tópico com o tenant_id e o id do JWT. Por isso o token precisa ser entregue ao Realtime
 * (`setAuth`) ANTES da assinatura.
 *
 * Sem Supabase configurado, em modo mock de autenticação ou sem sessão, não assina nada — e o
 * polling do resumo (alertasSummaryQuery) cobre. Falha de conexão nunca derruba a tela: o
 * realtime é conveniência. Com a API simulada, ouve o MOCK_ALERT_EVENT no lugar do canal.
 */
export function useAlertasRealtime() {
  const queryClient = useQueryClient()
  const router = useRouter()
  const userId = useAuthStore((s) => s.user?.id)
  const tenantId = useAuthStore((s) => s.user?.tenantId)

  useEffect(() => {
    if (!userId || !tenantId) return

    /** Caminho único para um alerta recebido, venha ele do canal real ou do simulado. */
    function onAlert(payload: unknown) {
      const alert = parseRealtimePayload(payload)
      if (!alert) return false

      notify.info(alert.title, {
        description: alert.description ?? undefined,
        action: {
          label: 'Ver',
          onClick: () => router.push(`${routes.alerts}/${alert.alertId}` as never),
        },
      })
      void queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.publications.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.navCounts })
      return true
    }

    if (env.useMocks) {
      const listener = (event: Event) => {
        const payload = (event as CustomEvent<unknown>).detail
        const parsed = parseRealtimePayload(payload)
        // Em produção o alerta já está no banco quando o broadcast chega; aqui o "banco" é o
        // store em memória, então o alerta é inserido antes de o cache ser invalidado.
        if (parsed) mockPushAlerta(parsed)
        onAlert(payload)
      }
      window.addEventListener(MOCK_ALERT_EVENT, listener)
      return () => window.removeEventListener(MOCK_ALERT_EVENT, listener)
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
          .channel(alertasChannelName(tenantId, userId), { config: { private: true } })
          .on('broadcast', { event: ALERT_CREATED_EVENT }, ({ payload }) => {
            onAlert(payload)
          })
          .subscribe()

        cleanup = () => void supabase.removeChannel(channel)
        // Se o efeito foi desmontado enquanto a assinatura era montada, desfaz agora.
        if (cancelled) cleanup()
      } catch {
        // Realtime é conveniência: o polling do resumo garante a convergência.
      }
    })()

    return () => {
      cancelled = true
      cleanup?.()
    }
  }, [queryClient, router, userId, tenantId])
}

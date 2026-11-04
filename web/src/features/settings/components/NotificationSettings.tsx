'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock } from 'lucide-react'
import { useState } from 'react'
import { ErrorFallback } from '@/components/shared/ErrorFallback'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { getErrorMessage } from '@/lib/api/errors'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { getNotificationPreferences, updateNotificationPreferences } from '../api'
import type { NotificationChannel, NotificationPreference } from '../types'
import { FormSkeleton } from './FormSkeleton'
import { SettingsSection } from './SettingsSection'

const channels: { key: NotificationChannel; label: string }[] = [
  { key: 'inApp', label: 'No app' },
  { key: 'email', label: 'E-mail' },
  { key: 'push', label: 'Push' },
]

function PreferencesForm({ initial }: { initial: NotificationPreference[] }) {
  const queryClient = useQueryClient()
  const [prefs, setPrefs] = useState(initial)
  const dirty = JSON.stringify(prefs) !== JSON.stringify(initial)

  const mutation = useMutation({
    mutationFn: updateNotificationPreferences,
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.notificationPreferences, saved)
      notify.success('Preferências de notificação salvas.')
    },
    onError: (error) =>
      notify.error('Não foi possível salvar.', { description: getErrorMessage(error) }),
  })

  function toggle(key: string, channel: NotificationChannel, value: boolean) {
    setPrefs((current) =>
      current.map((p) =>
        p.key === key ? { ...p, channels: { ...p.channels, [channel]: value } } : p
      )
    )
  }

  return (
    <SettingsSection
      title="Preferências de notificação"
      description="Escolha por onde receber cada tipo de aviso. Alertas de prazo no app não podem ser desativados."
      footer={
        <Button
          onClick={() => mutation.mutate(prefs)}
          disabled={!dirty || mutation.isPending}
          aria-busy={mutation.isPending}
        >
          {mutation.isPending ? <LoadingSpinner size="sm" decorative /> : null}
          Salvar preferências
        </Button>
      }
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-lg text-sm">
          <caption className="sr-only">Canais de notificação por tipo de evento</caption>
          <thead>
            <tr className="border-b text-left text-muted-foreground">
              <th scope="col" className="py-2 pr-4 font-medium">
                Evento
              </th>
              {channels.map((c) => (
                <th key={c.key} scope="col" className="w-20 px-2 py-2 text-center font-medium">
                  {c.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y">
            {prefs.map((pref) => (
              <tr key={pref.key}>
                <th scope="row" className="py-3 pr-4 text-left font-normal">
                  <span className="flex items-center gap-1.5 font-medium">
                    {pref.label}
                    {pref.locked ? (
                      <Lock className="size-3.5 text-muted-foreground" aria-label="Obrigatório" />
                    ) : null}
                  </span>
                  <span className="text-xs text-muted-foreground">{pref.description}</span>
                </th>
                {channels.map((c) => {
                  const locked = pref.locked === true && c.key === 'inApp'
                  return (
                    <td key={c.key} className="px-2 py-3 text-center">
                      <Switch
                        checked={pref.channels[c.key]}
                        disabled={locked}
                        onCheckedChange={(value) => toggle(pref.key, c.key, value)}
                        aria-label={`${pref.label} — ${c.label}`}
                      />
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SettingsSection>
  )
}

export function NotificationSettings() {
  const query = useQuery({
    queryKey: queryKeys.notificationPreferences,
    queryFn: getNotificationPreferences,
  })
  if (query.isPending) return <FormSkeleton fields={5} />
  if (query.isError)
    return <ErrorFallback error={query.error} onRetry={() => void query.refetch()} />
  return <PreferencesForm initial={query.data} />
}

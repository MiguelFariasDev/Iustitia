'use client'

import { useQuery } from '@tanstack/react-query'
import { Bot, Calendar, KeyRound, Landmark, MessageCircle, type LucideIcon } from 'lucide-react'
import { ErrorFallback } from '@/components/shared/ErrorFallback'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { queryKeys } from '@/lib/query/queryKeys'
import { listIntegrations } from '../api'
import type { IntegrationStatus } from '../types'

const icons: Record<string, LucideIcon> = {
  supabase: KeyRound,
  cnj: Landmark,
  anthropic: Bot,
  'google-calendar': Calendar,
  whatsapp: MessageCircle,
}

const statusConfig: Record<IntegrationStatus, { label: string; tone: Tone }> = {
  connected: { label: 'Conectado', tone: 'success' },
  available: { label: 'Disponível', tone: 'info' },
  soon: { label: 'Em breve', tone: 'neutral' },
}

export function IntegrationsSettings() {
  const query = useQuery({ queryKey: queryKeys.integrations, queryFn: listIntegrations })

  if (query.isPending) {
    return (
      <div className="grid gap-4 md:grid-cols-2" role="status" aria-label="Carregando integrações">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-44 rounded-xl" />
        ))}
      </div>
    )
  }
  if (query.isError)
    return <ErrorFallback error={query.error} onRetry={() => void query.refetch()} />

  return (
    <ul className="grid gap-4 md:grid-cols-2">
      {query.data.map((integration) => {
        const Icon = icons[integration.id] ?? KeyRound
        const status = statusConfig[integration.status]
        return (
          <li key={integration.id}>
            <Card className="h-full">
              <CardHeader className="flex-row items-start gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted">
                  <Icon className="size-5" aria-hidden />
                </span>
                <div className="min-w-0 flex-1 space-y-1">
                  <CardTitle className="text-base">
                    <h2>{integration.name}</h2>
                  </CardTitle>
                  <p className="text-xs text-muted-foreground">{integration.category}</p>
                </div>
                <StatusBadge tone={status.tone}>{status.label}</StatusBadge>
              </CardHeader>
              <CardContent>
                <CardDescription>{integration.description}</CardDescription>
              </CardContent>
              <CardFooter className="mt-auto">
                <Button variant="outline" size="sm" disabled={integration.status !== 'available'}>
                  {integration.status === 'connected' ? 'Gerenciado pela plataforma' : 'Conectar'}
                </Button>
              </CardFooter>
            </Card>
          </li>
        )
      })}
    </ul>
  )
}

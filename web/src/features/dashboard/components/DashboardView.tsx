'use client'

import { useSuspenseQuery } from '@tanstack/react-query'
import { ArrowRight, Bell, Briefcase, CalendarDays, Newspaper, ScanEye, Users } from 'lucide-react'
import Link from 'next/link'
import { EmptyState } from '@/components/shared/EmptyState'
import { MetricCard } from '@/components/shared/MetricCard'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { routes } from '@/config/routes'
import { apiClient } from '@/lib/api/client'
import { formatRelative } from '@/lib/format'
import { dashboardSummaryQuery } from '../queries'
import type { DashboardSummary } from '../types'

export function DashboardMetrics({ metrics }: { metrics: DashboardSummary['metrics'] }) {
  return (
    <section aria-labelledby="metrics-heading">
      <h2 id="metrics-heading" className="sr-only">
        Indicadores
      </h2>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {/*
          Cada cartão leva à lista JÁ FILTRADA pelo que ele conta. Levar para a lista sem
          filtro faria o clique abrir um número diferente do que o cartão mostrava — e
          obrigaria a refazer o filtro à mão.
        */}
        <MetricCard
          label="Processos ativos"
          icon={Briefcase}
          href={`${routes.processes}?status=Ativo`}
          hrefLabel="Processos ativos — ver a lista"
          {...metrics.activeProcesses}
        />
        <MetricCard
          label="Publicações não lidas"
          icon={Newspaper}
          increaseIsGood={false}
          href={`${routes.publications}?isRead=false`}
          hrefLabel="Publicações não lidas — ver a lista"
          {...metrics.unreadPublications}
        />
        <MetricCard
          label="Aguardando revisão humana"
          icon={ScanEye}
          increaseIsGood={false}
          hint="publicações a revisar"
          // "untreated" é como a tela chama o PendingReview do domínio (ver contract.ts).
          href={`${routes.publications}?status=untreated`}
          hrefLabel="Publicações aguardando revisão — ver a lista"
          {...metrics.pendingReviews}
        />
        <MetricCard
          label="Alertas"
          icon={Bell}
          increaseIsGood={false}
          hint="não lidos"
          href={routes.alerts}
          hrefLabel="Alertas não lidos — ver a lista"
          {...metrics.unreadAlerts}
        />
      </div>
    </section>
  )
}

/**
 * Atalhos para os módulos das próximas fases. Sem número de propósito: agenda e atendimentos
 * não existem no backend, e um contador aqui seria inventado. Cada atalho é um link de
 * verdade — o cartão inteiro clicável, com foco visível.
 */
const atalhos = [
  {
    label: 'Agenda',
    descricao: 'Audiências e compromissos',
    icon: CalendarDays,
    href: routes.agenda,
  },
  {
    label: 'Atendimentos',
    descricao: 'Relacionamento com clientes',
    icon: Users,
    href: routes.serviceDesk,
  },
] as const

export function DashboardAtalhos() {
  return (
    <section aria-labelledby="atalhos-heading">
      <h2 id="atalhos-heading" className="sr-only">
        Atalhos
      </h2>
      <div className="grid gap-4 sm:grid-cols-2">
        {atalhos.map(({ label, descricao, icon: Icon, href }) => (
          <Link
            key={label}
            href={href as never}
            className="group flex items-center gap-3 rounded-xl border bg-card p-4 transition-shadow hover:shadow-md focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          >
            <Icon className="size-5 text-muted-foreground" aria-hidden />
            <span className="flex-1">
              <span className="block text-sm font-medium">{label}</span>
              <span className="block text-xs text-muted-foreground">{descricao} — em breve</span>
            </span>
            <ArrowRight
              className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5"
              aria-hidden
            />
          </Link>
        ))}
      </div>
    </section>
  )
}

export function DashboardView() {
  const { data } = useSuspenseQuery(dashboardSummaryQuery(apiClient))

  return (
    <div className="space-y-6">
      <DashboardMetrics metrics={data.metrics} />

      <DashboardAtalhos />

      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-2">
            <div className="space-y-1.5">
              <CardTitle className="text-base">
                <h2 id="publications-heading">Publicações não lidas</h2>
              </CardTitle>
              <CardDescription>Capturadas automaticamente dos diários oficiais.</CardDescription>
            </div>
            <Button variant="ghost" size="sm" asChild>
              <Link href={`${routes.publications}?isRead=false` as never}>
                Ver todas<span className="sr-only"> as publicações não lidas</span>
              </Link>
            </Button>
          </CardHeader>
          <CardContent>
            {data.publications.length === 0 ? (
              <EmptyState icon={Newspaper} title="Nenhuma publicação pendente" />
            ) : (
              <ul className="divide-y" aria-labelledby="publications-heading">
                {data.publications.map((p) => (
                  <li key={p.id}>
                    <Link
                      href={`${routes.publications}/${p.id}` as never}
                      className="block space-y-1.5 rounded-md py-3 outline-none first:pt-0 last:pb-0 hover:bg-accent/40 focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      <span className="flex flex-wrap items-center gap-2">
                        <StatusBadge tone="info">{p.source}</StatusBadge>
                        <span className="font-mono text-xs text-muted-foreground">
                          {p.processNumber}
                        </span>
                        <time
                          dateTime={p.publishedAt}
                          className="ml-auto text-xs text-muted-foreground"
                        >
                          {formatRelative(p.publishedAt)}
                        </time>
                      </span>
                      <span className="block text-sm font-medium">{p.client}</span>
                      <span className="line-clamp-2 block text-sm text-muted-foreground">
                        {p.excerpt}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-2">
            <div className="space-y-1.5">
              <CardTitle className="text-base">
                <h2 id="alerts-heading">Alertas</h2>
              </CardTitle>
              <CardDescription>O que chegou e ainda não foi visto.</CardDescription>
            </div>
            <Button variant="ghost" size="sm" asChild>
              <Link href={routes.alerts}>
                Ver todos<span className="sr-only"> os alertas</span>
              </Link>
            </Button>
          </CardHeader>
          <CardContent>
            {data.alerts.length === 0 ? (
              <EmptyState icon={Bell} title="Nenhum alerta novo" />
            ) : (
              <ul className="divide-y" aria-labelledby="alerts-heading">
                {data.alerts.map((a) => (
                  <li key={a.id}>
                    <Link
                      href={`${routes.alerts}/${a.id}` as never}
                      className="block space-y-1 rounded-md py-3 outline-none first:pt-0 last:pb-0 hover:bg-accent/40 focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      <span className="flex items-center gap-2">
                        <span className="text-sm font-medium">{a.title}</span>
                        <time
                          dateTime={a.createdAt}
                          className="ml-auto text-xs text-muted-foreground"
                        >
                          {formatRelative(a.createdAt)}
                        </time>
                      </span>
                      {a.description ? (
                        <span className="line-clamp-2 block text-sm text-muted-foreground">
                          {a.description}
                        </span>
                      ) : null}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

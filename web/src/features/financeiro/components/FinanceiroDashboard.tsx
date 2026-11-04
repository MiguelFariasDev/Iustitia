'use client'

import type { ColumnDef } from '@tanstack/react-table'
import dynamic from 'next/dynamic'
import { useMemo } from 'react'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatCurrency, formatDate } from '@/lib/format'
import { calcularResumo, fluxoCaixaMock, lancamentosMock } from '../mocks'
import type { Lancamento, StatusLancamento, TipoLancamento } from '../types'

// Recharts é pesado e só a aba "Fluxo de caixa" o usa: carrega sob demanda.
const FluxoCaixaChart = dynamic(() => import('./FluxoCaixaChart').then((m) => m.FluxoCaixaChart), {
  ssr: false,
  loading: () => <div className="h-72 animate-pulse rounded-lg bg-muted" />,
})

const tomStatus: Record<StatusLancamento, Tone> = {
  Pendente: 'warning',
  Pago: 'success',
  Atrasado: 'danger',
}

const colunas: ColumnDef<Lancamento, unknown>[] = [
  { accessorKey: 'descricao', header: 'Descrição' },
  { accessorKey: 'cliente', header: 'Cliente', cell: ({ row }) => row.original.cliente ?? '—' },
  {
    accessorKey: 'processo',
    header: 'Processo',
    cell: ({ row }) => <span className="font-mono text-xs">{row.original.processo ?? '—'}</span>,
  },
  { accessorKey: 'valor', header: 'Valor', cell: ({ row }) => formatCurrency(row.original.valor) },
  {
    accessorKey: 'vencimento',
    header: 'Vencimento',
    cell: ({ row }) => formatDate(row.original.vencimento),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <StatusBadge tone={tomStatus[row.original.status]}>{row.original.status}</StatusBadge>
    ),
  },
]

const abas: { valor: string; rotulo: string; tipo: TipoLancamento }[] = [
  { valor: 'honorarios', rotulo: 'Honorários', tipo: 'Honorario' },
  { valor: 'pagar', rotulo: 'Contas a pagar', tipo: 'ContaPagar' },
  { valor: 'receber', rotulo: 'Contas a receber', tipo: 'ContaReceber' },
]

export function FinanceiroDashboard() {
  const resumo = useMemo(() => calcularResumo(lancamentosMock), [])

  const cards = [
    ['Honorários a receber', resumo.honorariosAReceber],
    ['Honorários recebidos (mês)', resumo.honorariosRecebidosMes],
    ['Contas a pagar', resumo.contasAPagar],
    ['Contas a receber', resumo.contasAReceber],
    ['Saldo', resumo.saldo],
  ] as const

  return (
    <div className="space-y-6">
      <p
        role="note"
        className="rounded-lg border border-dashed px-3 py-2 text-sm text-muted-foreground"
      >
        <strong>Dados de demonstração.</strong> O módulo Financeiro ainda não está ligado ao
        servidor — nada aqui é do seu escritório, e cadastrar lançamentos ainda não é possível.
      </p>

      <section aria-label="Resumo financeiro" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map(([rotulo, valor]) => (
          <Card key={rotulo}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{rotulo}</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-semibold tabular-nums">
              {formatCurrency(valor)}
            </CardContent>
          </Card>
        ))}
      </section>

      <Tabs defaultValue="honorarios">
        <TabsList>
          {abas.map((a) => (
            <TabsTrigger key={a.valor} value={a.valor}>
              {a.rotulo}
            </TabsTrigger>
          ))}
          <TabsTrigger value="fluxo">Fluxo de caixa</TabsTrigger>
        </TabsList>

        {abas.map((a) => (
          <TabsContent key={a.valor} value={a.valor}>
            <DataTable
              caption={a.rotulo}
              columns={colunas}
              data={lancamentosMock.filter((l) => l.tipo === a.tipo)}
              getRowId={(l) => l.id}
              searchPlaceholder="Buscar por descrição ou cliente"
              emptyTitle="Nenhum lançamento"
            />
          </TabsContent>
        ))}

        <TabsContent value="fluxo">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Entradas e saídas — últimos 6 meses</CardTitle>
            </CardHeader>
            <CardContent>
              <FluxoCaixaChart dados={fluxoCaixaMock} />
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}

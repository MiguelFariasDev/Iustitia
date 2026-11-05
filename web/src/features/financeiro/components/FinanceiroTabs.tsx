'use client'

import { Download } from 'lucide-react'
import dynamic from 'next/dynamic'
import { useMemo, useState } from 'react'
import { FilterChip } from '@/components/shared/FilterChip'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { downloadCsv } from '@/lib/csv'
import { notify } from '@/lib/notify'
import { lancamentosToCsv } from '../export'
import { filtrarLancamentos } from '../mocks'
import {
  periodoFinanceiroLabels,
  statusLancamento,
  type FluxoCaixaMes,
  type Lancamento,
  type PeriodoFinanceiro,
  type StatusLancamento,
  type TipoLancamento,
} from '../types'
import { LancamentosTable } from './LancamentosTable'

// Recharts é pesado e só a aba "Fluxo de caixa" o usa: carrega sob demanda.
const FluxoCaixaChart = dynamic(() => import('./FluxoCaixaChart').then((m) => m.FluxoCaixaChart), {
  ssr: false,
  loading: () => <div className="h-72 animate-iu-pulse rounded-lg bg-border" />,
})

type Aba = 'honorarios' | 'pagar' | 'receber' | 'fluxo'

const abas: { valor: Exclude<Aba, 'fluxo'>; rotulo: string; tipo: TipoLancamento }[] = [
  { valor: 'honorarios', rotulo: 'Honorários', tipo: 'Honorario' },
  { valor: 'pagar', rotulo: 'Contas a pagar', tipo: 'ContaPagar' },
  { valor: 'receber', rotulo: 'Contas a receber', tipo: 'ContaReceber' },
]

interface FinanceiroTabsProps {
  lancamentos: Lancamento[]
  fluxoCaixa: FluxoCaixaMes[]
}

export function FinanceiroTabs({ lancamentos, fluxoCaixa }: FinanceiroTabsProps) {
  const [aba, setAba] = useState<Aba>('honorarios')
  const [status, setStatus] = useState<StatusLancamento | null>(null)
  const [periodo, setPeriodo] = useState<PeriodoFinanceiro>('todos')

  const filtrados = useMemo(
    () => filtrarLancamentos(lancamentos, { status, periodo }),
    [lancamentos, status, periodo]
  )

  const abaAtual = abas.find((a) => a.valor === aba)

  function exportar(formato: 'csv' | 'pdf') {
    if (!abaAtual) return

    if (formato === 'pdf') {
      // MOCK: não há gerador de PDF. Dizer isso é melhor que baixar um arquivo vazio.
      notify.info('Exportação em PDF ainda não disponível.', {
        description: 'Use o CSV por enquanto.',
      })
      return
    }

    const linhas = filtrados.filter((l) => l.tipo === abaAtual.tipo)
    downloadCsv(`financeiro-${abaAtual.valor}.csv`, lancamentosToCsv(linhas))
  }

  return (
    <Tabs value={aba} onValueChange={(valor) => setAba(valor as Aba)}>
      <TabsList>
        {abas.map((a) => (
          <TabsTrigger key={a.valor} value={a.valor}>
            {a.rotulo}
          </TabsTrigger>
        ))}
        <TabsTrigger value="fluxo">Fluxo de caixa</TabsTrigger>
      </TabsList>

      {aba !== 'fluxo' ? (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex flex-wrap gap-2" role="group" aria-label="Filtrar por status">
              {statusLancamento.map((valor) => (
                <FilterChip
                  key={valor}
                  active={status === valor}
                  onClick={() => setStatus(status === valor ? null : valor)}
                >
                  {valor}
                </FilterChip>
              ))}
            </div>

            <Select value={periodo} onValueChange={(v) => setPeriodo(v as PeriodoFinanceiro)}>
              <SelectTrigger className="w-56" aria-label="Período de vencimento">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {(Object.keys(periodoFinanceiroLabels) as PeriodoFinanceiro[]).map((valor) => (
                  <SelectItem key={valor} value={valor}>
                    {periodoFinanceiroLabels[valor]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline">
                <Download aria-hidden />
                Exportar
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => exportar('csv')}>Exportar em CSV</DropdownMenuItem>
              <DropdownMenuItem onSelect={() => exportar('pdf')}>
                Exportar em PDF (em breve)
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      ) : null}

      {abas.map((a) => (
        <TabsContent key={a.valor} value={a.valor}>
          <LancamentosTable tipo={a.tipo} lancamentos={filtrados} />
        </TabsContent>
      ))}

      <TabsContent value="fluxo">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Entradas e saídas — últimos 6 meses</CardTitle>
          </CardHeader>
          <CardContent>
            <FluxoCaixaChart dados={fluxoCaixa} />
          </CardContent>
        </Card>
      </TabsContent>
    </Tabs>
  )
}

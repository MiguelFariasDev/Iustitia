'use client'

import { useState } from 'react'
import { KpiCard } from '@/components/shared/KpiCard'
import { ToneOutlineButton } from '@/components/shared/ToneOutlineButton'
import { cn } from '@/lib/utils'
import {
  brl,
  conciliacaoInicial,
  financeiroKpis,
  FLUXO_ALTURA_PX,
  FLUXO_ESCALA_MAX,
  fluxoCaixa,
} from '../demo'
import { DemoSection } from './DemoSection'

/** Altura da barra em px: `v / 170 * 140`, como no protótipo (170 mil = barra cheia). */
export const alturaBarra = (valor: number) =>
  Math.round((valor / FLUXO_ESCALA_MAX) * FLUXO_ALTURA_PX)

/** Painel do financeiro. Todo o conteúdo é demonstração (o módulo financeiro não tem backend). */
export function FinanceiroPanel() {
  const [conciliados, setConciliados] = useState<ReadonlySet<string>>(new Set())

  function conciliar(id: string) {
    setConciliados((atual) => new Set(atual).add(id))
  }

  return (
    <div className="space-y-6">
      <section
        aria-label="Indicadores"
        className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3"
      >
        <KpiCard
          label="A receber hoje"
          value={brl(financeiroKpis.receberHoje)}
          hint="demonstração"
          tone="brand"
        />
        <KpiCard
          label="Em atraso"
          value={brl(financeiroKpis.emAtraso)}
          hint="demonstração"
          tone="danger"
        />
        <KpiCard
          label="Recebido no mês"
          value={brl(financeiroKpis.recebidoMes)}
          hint="demonstração"
          tone="brand"
        />
        <KpiCard
          label="A pagar em 7 dias"
          value={brl(financeiroKpis.pagar7Dias)}
          hint="demonstração"
          tone="warning"
        />
      </section>

      <DemoSection id="fluxo-titulo" title="Entradas e saídas" demo>
        <figure aria-label="Gráfico de entradas e saídas, de junho a novembro">
          <div
            className="flex items-end gap-4"
            style={{ height: FLUXO_ALTURA_PX + 24 }}
            aria-hidden
          >
            {fluxoCaixa.map((mes) => (
              <div key={mes.mes} className="flex flex-1 flex-col items-center gap-1">
                <div className="flex items-end gap-1" style={{ height: FLUXO_ALTURA_PX }}>
                  <div
                    className="w-4 rounded-t-xs bg-brand"
                    style={{ height: alturaBarra(mes.entradas) }}
                  />
                  <div
                    className="w-4 rounded-t-xs bg-destructive"
                    style={{ height: alturaBarra(mes.saidas) }}
                  />
                </div>
                <span className="text-xs text-muted-foreground">{mes.mes}</span>
              </div>
            ))}
          </div>
          <figcaption className="mt-2 flex gap-4 text-xs text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <span className="size-2 rounded-full bg-brand" aria-hidden /> Entradas
            </span>
            <span className="flex items-center gap-1.5">
              <span className="size-2 rounded-full bg-destructive" aria-hidden /> Saídas
            </span>
          </figcaption>
          {/* Alternativa em texto para o gráfico (leitor de tela). */}
          <table className="sr-only">
            <caption>Entradas e saídas por mês, em milhares de reais</caption>
            <thead>
              <tr>
                <th>Mês</th>
                <th>Entradas</th>
                <th>Saídas</th>
              </tr>
            </thead>
            <tbody>
              {fluxoCaixa.map((mes) => (
                <tr key={mes.mes}>
                  <td>{mes.mes}</td>
                  <td>{mes.entradas}</td>
                  <td>{mes.saidas}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </figure>
      </DemoSection>

      <DemoSection id="conciliacao-titulo" title="Conciliação" demo>
        <ul className="divide-y">
          {conciliacaoInicial.map((item) => {
            const feito = conciliados.has(item.id)

            return (
              <li
                key={item.id}
                className={cn(
                  'flex items-center gap-3 py-3 first:pt-0 last:pb-0',
                  feito && 'opacity-50'
                )}
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium">{item.descricao}</span>
                  <span className="block text-xs text-muted-foreground">{item.data}</span>
                </span>
                <span className="font-mono text-sm">{brl(item.valor)}</span>
                <ToneOutlineButton
                  tone="ok"
                  disabled={feito}
                  onClick={() => conciliar(item.id)}
                  aria-label={`${feito ? 'Conciliado' : 'Conciliar'}: ${item.descricao}`}
                >
                  {feito ? 'Conciliado' : 'Conciliar'}
                </ToneOutlineButton>
              </li>
            )
          })}
        </ul>
      </DemoSection>
    </div>
  )
}

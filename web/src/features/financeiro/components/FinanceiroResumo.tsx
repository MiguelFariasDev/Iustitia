import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { FinanceiroResumo as Resumo } from '../types'

interface FinanceiroResumoProps {
  resumo: Resumo
}

/** Quatro números que respondem "como está o caixa?". Saldo negativo é dito, não só colorido. */
export function FinanceiroResumo({ resumo }: FinanceiroResumoProps) {
  const cartoes = [
    {
      rotulo: 'Faturamento do mês',
      valor: resumo.faturamentoMes,
      dica: 'honorários recebidos',
      borda: 'border-t-brand',
    },
    {
      rotulo: 'Honorários a receber',
      valor: resumo.honorariosAReceber,
      dica: 'em aberto',
      borda: 'border-t-warning',
    },
    {
      rotulo: 'Contas a pagar',
      valor: resumo.contasAPagar,
      dica: 'em aberto',
      borda: 'border-t-destructive',
    },
    {
      rotulo: 'Saldo',
      valor: resumo.saldo,
      dica: 'recebido menos pago',
      borda: 'border-t-line-strong',
    },
  ]

  return (
    <section aria-label="Resumo financeiro" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cartoes.map(({ rotulo, valor, dica, borda }) => (
        <Card key={rotulo} className={cn('border-t-2', borda)}>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">{rotulo}</CardTitle>
          </CardHeader>
          <CardContent>
            <p
              className={cn(
                'text-2xl font-semibold tabular-nums',
                valor < 0 && 'text-destructive-foreground'
              )}
            >
              {formatCurrency(valor)}
              {valor < 0 ? <span className="sr-only"> (negativo)</span> : null}
            </p>
            <p className="text-xs text-muted-foreground">{dica}</p>
          </CardContent>
        </Card>
      ))}
    </section>
  )
}

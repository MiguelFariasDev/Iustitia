'use client'

import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { formatCurrency } from '@/lib/format'
import type { FluxoCaixaMes } from '../types'

export function FluxoCaixaChart({ dados }: { dados: FluxoCaixaMes[] }) {
  return (
    <div
      role="img"
      aria-label="Gráfico de barras de entradas e saídas dos últimos seis meses"
      className="h-72 w-full"
    >
      <ResponsiveContainer>
        <BarChart data={dados}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="mes" />
          <YAxis tickFormatter={(v: number) => `${v / 1000}k`} />
          <Tooltip formatter={(v) => formatCurrency(Number(v))} />
          <Legend />
          <Bar
            dataKey="entradas"
            name="Entradas"
            fill="var(--color-chart-2, #16a34a)"
            radius={[4, 4, 0, 0]}
          />
          <Bar
            dataKey="saidas"
            name="Saídas"
            fill="var(--color-chart-5, #dc2626)"
            radius={[4, 4, 0, 0]}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

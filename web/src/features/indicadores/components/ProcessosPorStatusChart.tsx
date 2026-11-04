'use client'

import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { formatNumber } from '@/lib/format'
import { descreverSerie } from '../descricao'
import { paleta } from '../palette'
import type { ItemSerie } from '../types'

export function ProcessosPorStatusChart({ dados }: { dados: ItemSerie[] }) {
  return (
    <div
      role="img"
      aria-label={descreverSerie('Gráfico de barras dos processos por status', dados)}
      className="h-72 w-full"
    >
      <ResponsiveContainer>
        <BarChart data={dados} accessibilityLayer={false}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="rotulo" />
          <YAxis allowDecimals={false} />
          <Tooltip formatter={(valor) => formatNumber(Number(valor))} />
          <Bar
            dataKey="total"
            name="Processos"
            fill={paleta[0]}
            radius={[4, 4, 0, 0]}
            isAnimationActive={false}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

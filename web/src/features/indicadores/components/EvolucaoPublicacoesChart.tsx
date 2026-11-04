'use client'

import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { formatNumber } from '@/lib/format'
import { descreverSerie } from '../descricao'
import { paleta } from '../palette'
import type { ItemSerie } from '../types'

export function EvolucaoPublicacoesChart({ dados }: { dados: ItemSerie[] }) {
  return (
    <div
      role="img"
      aria-label={descreverSerie('Gráfico de linha da evolução de publicações', dados)}
      className="h-72 w-full"
    >
      <ResponsiveContainer>
        <LineChart data={dados} accessibilityLayer={false}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="rotulo" />
          <YAxis allowDecimals={false} />
          <Tooltip formatter={(valor) => formatNumber(Number(valor))} />
          <Line
            type="monotone"
            dataKey="total"
            name="Publicações"
            stroke={paleta[0]}
            strokeWidth={2}
            dot={{ r: 3 }}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

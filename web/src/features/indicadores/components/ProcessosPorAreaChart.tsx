'use client'

import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'
import { formatNumber } from '@/lib/format'
import { descreverSerie } from '../descricao'
import { paleta } from '../palette'
import type { ItemSerie } from '../types'

export function ProcessosPorAreaChart({ dados }: { dados: ItemSerie[] }) {
  return (
    <div
      role="img"
      aria-label={descreverSerie('Gráfico de rosca dos processos por área', dados)}
      className="h-72 w-full"
    >
      <ResponsiveContainer>
        <PieChart accessibilityLayer={false}>
          <Pie
            data={dados}
            dataKey="total"
            nameKey="rotulo"
            innerRadius="55%"
            outerRadius="85%"
            paddingAngle={2}
            isAnimationActive={false}
          >
            {dados.map((item, i) => (
              <Cell key={item.rotulo} fill={paleta[i % paleta.length]} />
            ))}
          </Pie>
          <Tooltip formatter={(valor) => formatNumber(Number(valor))} />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </div>
  )
}

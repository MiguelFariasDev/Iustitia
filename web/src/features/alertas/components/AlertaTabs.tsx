'use client'

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { tabCount, tabLabels } from '../tabs'
import { alertaTabs, type AlertaTab, type AlertasSummary } from '../types'

interface AlertaTabsProps {
  value: AlertaTab
  onChange: (tab: AlertaTab) => void
  summary: AlertasSummary | undefined
  /** O painel da aba ativa — o mesmo para todas, já filtrado (ver AlertasView). */
  children: React.ReactNode
}

/**
 * Abas com a contagem no próprio rótulo ("Importantes (2)"): o número faz parte do nome
 * acessível, então leitores de tela anunciam a contagem junto com a aba. Radix cuida de
 * role=tablist/tab/tabpanel e das setas do teclado.
 */
export function AlertaTabs({ value, onChange, summary, children }: AlertaTabsProps) {
  return (
    <Tabs value={value} onValueChange={(next) => onChange(next as AlertaTab)}>
      <TabsList
        aria-label="Categorias de alertas"
        className="max-w-full justify-start overflow-x-auto"
      >
        {alertaTabs.map((tab) => {
          const count = tabCount(tab, summary)
          return (
            <TabsTrigger key={tab} value={tab}>
              {tabLabels[tab]}
              {count !== null ? ` (${count})` : ''}
            </TabsTrigger>
          )
        })}
      </TabsList>
      {/*
        Um TabsContent por aba, todos com o MESMO conteúdo: o Radix só monta o da aba ativa. É o
        que liga cada aba ao seu painel (aria-controls/aria-labelledby) — sem os painéis, os
        ids referenciados pelas abas não existiriam e o leitor de tela perderia a relação.
      */}
      {alertaTabs.map((tab) => (
        <TabsContent key={tab} value={tab} className="animate-iu-in pt-2">
          {children}
        </TabsContent>
      ))}
    </Tabs>
  )
}

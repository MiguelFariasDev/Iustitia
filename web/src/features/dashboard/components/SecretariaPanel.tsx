'use client'

import Link from 'next/link'
import { useState } from 'react'
import { KpiCard } from '@/components/shared/KpiCard'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'
import {
  aguardandoHumano,
  checklistProtocoloInicial,
  secretariaKpis,
  type ChecklistItem,
} from '../demo'
import { DemoSection } from './DemoSection'

/**
 * Painel de apoio administrativo. TODO o conteúdo é demonstração: a API só admite advogado e sócio
 * em publicações e processos (ver ADR-068), então este perfil não tem de onde tirar dado real
 * ainda — e a tela diz isso em vez de parecer pronta.
 */
export function SecretariaPanel() {
  const [checklist, setChecklist] = useState<ChecklistItem[]>(() =>
    checklistProtocoloInicial.map((item) => ({ ...item }))
  )
  const aProtocolar = checklist.filter((item) => !item.feito).length

  function alternar(id: string) {
    setChecklist((lista) =>
      lista.map((item) => (item.id === id ? { ...item, feito: !item.feito } : item))
    )
  }

  return (
    <div className="space-y-6">
      <section
        aria-label="Indicadores"
        className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3"
      >
        <KpiCard
          label="Publicações sem processo"
          value={secretariaKpis.publicacoesSemProcesso}
          hint="demonstração"
          tone="warning"
        />
        <KpiCard
          label="Atendimentos aguardando"
          value={secretariaKpis.atendimentosAguardando}
          hint="transferidos pela IA"
          tone="ia"
          href={routes.serviceDesk}
          hrefLabel="Atendimentos aguardando — abrir atendimentos"
        />
        <KpiCard label="A protocolar" value={aProtocolar} hint="checklist de hoje" tone="danger" />
        <KpiCard
          label="Retornos de ligação"
          value={secretariaKpis.retornosLigacao}
          hint="demonstração"
          tone="neutral"
        />
      </section>

      <div className="grid gap-6 lg:grid-cols-2">
        <DemoSection id="humano-titulo" title="Aguardando atendimento humano" demo>
          <ul className="divide-y">
            {aguardandoHumano.map((item) => (
              <li key={item.id}>
                <Link
                  href={routes.serviceDesk}
                  className="flex items-center gap-3 rounded-md py-3 outline-none first:pt-0 last:pb-0 hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{item.cliente}</span>
                    <span className="block truncate text-xs text-muted-foreground">
                      {item.motivo}
                    </span>
                  </span>
                  <span className="font-mono text-xs text-muted-foreground">{item.espera}</span>
                </Link>
              </li>
            ))}
          </ul>
        </DemoSection>

        <DemoSection id="protocolo-titulo" title="Checklist de protocolo" demo>
          <ul className="space-y-2">
            {checklist.map((item) => (
              <li key={item.id}>
                <label className="flex cursor-pointer items-center gap-3 rounded-md py-1 text-sm has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring">
                  <input
                    type="checkbox"
                    checked={item.feito}
                    onChange={() => alternar(item.id)}
                    className="size-5 shrink-0 cursor-pointer appearance-none rounded-md border-[1.5px] border-line-strong checked:border-brand checked:bg-brand"
                  />
                  <span className={cn(item.feito && 'text-muted-foreground line-through')}>
                    {item.texto}
                  </span>
                </label>
              </li>
            ))}
          </ul>
        </DemoSection>
      </div>
    </div>
  )
}

'use client'

import { useQuery } from '@tanstack/react-query'
import { FileSpreadsheet, PenLine, Rocket } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { ProcessoImportWizard } from '@/features/processos/components/ProcessoImportWizard'
import { listProcessos } from '@/features/processos/api'
import { queryKeys } from '@/lib/query/queryKeys'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

type Mode = null | 'csv'

/**
 * Passo 5: três caminhos. A importação reaproveita o assistente que já existe em /processos/importar
 * (modelo CSV, prévia com linhas inválidas destacadas, relatório). Como o assistente é autônomo, o
 * passo detecta o resultado pela CONTAGEM de processos (atualizada a cada poucos segundos).
 */
export function StepProcesses({ onComplete, onSkip, onBack }: StepProps) {
  const [mode, setMode] = useState<Mode>(null)
  const count = useQuery({
    queryKey: queryKeys.processos.list({ onboarding: true }),
    queryFn: () => listProcessos({ page: 1, pageSize: 1 }),
    refetchInterval: 4000,
    refetchOnWindowFocus: true,
  })
  const total = count.data?.totalCount ?? 0

  return (
    <div className="space-y-5">
      <div className="grid gap-3 sm:grid-cols-3">
        <Choice
          icon={FileSpreadsheet}
          title="Importar planilha (CSV)"
          text="Baixe o modelo, preencha e envie. Você vê a prévia e os erros antes de confirmar."
          selected={mode === 'csv'}
          onSelect={() => setMode(mode === 'csv' ? null : 'csv')}
        />
        <div className="iu-card iu-lift flex flex-col p-4">
          <PenLine className="mb-2 size-5 text-[var(--iu-brand)]" aria-hidden />
          <p className="text-sm font-semibold">Adicionar manualmente</p>
          <p className="mt-1 flex-1 text-sm text-muted-foreground">
            Número CNJ, cliente, área, vara e responsável. A Athena passa a acompanhar o processo.
          </p>
          <Button asChild variant="outline" className="mt-3 h-11">
            <Link href="/processos/novo">Cadastrar um processo</Link>
          </Button>
        </div>
        <Choice
          icon={Rocket}
          title="Começar sem processos"
          text="A Athena cadastra pelo monitoramento. Você pode adicionar quando quiser."
          selected={false}
          onSelect={() => onComplete({ processes: 'later' })}
          cta="Continuar assim"
        />
      </div>

      {mode === 'csv' ? (
        <div className="iu-in rounded-[14px] border p-4">
          <ProcessoImportWizard />
        </div>
      ) : null}

      {total > 0 ? (
        <p role="status" className="iu-in rounded-[10px] bg-[var(--iu-brand-soft)] p-3 text-sm">
          Encontramos <strong>{total}</strong> {total === 1 ? 'processo cadastrado' : 'processos cadastrados'}. Pode seguir.
        </p>
      ) : null}

      <StepActions
        onBack={onBack}
        onSkip={onSkip}
        onContinue={() => onComplete({ processes: total > 0 ? (mode === 'csv' ? 'csv' : 'manual') : 'later' })}
        continueLabel={total > 0 ? 'Continuar' : 'Continuar sem processos'}
      />
    </div>
  )
}

function Choice({
  icon: Icon,
  title,
  text,
  selected,
  onSelect,
  cta,
}: {
  icon: typeof Rocket
  title: string
  text: string
  selected: boolean
  onSelect: () => void
  cta?: string
}) {
  return (
    <div className={`iu-card iu-lift flex flex-col p-4 ${selected ? 'border-[var(--iu-brand)]' : ''}`}>
      <Icon className="mb-2 size-5 text-[var(--iu-brand)]" aria-hidden />
      <p className="text-sm font-semibold">{title}</p>
      <p className="mt-1 flex-1 text-sm text-muted-foreground">{text}</p>
      <Button type="button" variant="outline" className="mt-3 h-11" onClick={onSelect} aria-pressed={cta ? undefined : selected}>
        {cta ?? (selected ? 'Fechar' : 'Importar')}
      </Button>
    </div>
  )
}

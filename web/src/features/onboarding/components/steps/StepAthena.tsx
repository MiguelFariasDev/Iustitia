'use client'

import { Lock } from 'lucide-react'
import { useState } from 'react'
import { Switch } from '@/components/ui/switch'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

const leadOptions = [
  { value: 1, label: '1 dia' },
  { value: 3, label: '3 dias' },
  { value: 7, label: '7 dias' },
] as const

/** Passo 3: preferências da Athena. A revisão humana aparece TRAVADA: é regra de produto, não opção. */
export function StepAthena({ state, onComplete, onSkip, onBack }: StepProps) {
  const initial = state.data.athena
  const [suggestDeadlines, setSuggest] = useState(initial?.suggestDeadlines ?? true)
  const [notifyUnlinked, setNotify] = useState(initial?.notifyUnlinked ?? true)
  const [lead, setLead] = useState<1 | 3 | 7>(initial?.alertLeadDays ?? 3)

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        onComplete({ athena: { suggestDeadlines, notifyUnlinked, alertLeadDays: lead } })
      }}
      className="space-y-5"
    >
      <div className="iu-card flex items-start gap-3 border-[var(--iu-accent)] bg-[var(--iu-accent-soft)] p-4">
        <Lock className="mt-0.5 size-5 shrink-0 text-[var(--iu-accent)]" aria-hidden />
        <div>
          <p className="text-sm font-semibold">Revisão humana: sempre ativa</p>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Nada é salvo sem decisão do advogado. Esta regra não pode ser desligada.
          </p>
        </div>
        <Switch checked disabled aria-label="Revisão humana obrigatória (sempre ativa)" className="ml-auto" />
      </div>

      <SettingRow
        id="suggest"
        title="Sugerir prazo automaticamente"
        text="A Athena propõe o tipo de ato e o prazo para cada publicação. Você aceita, corrige ou rejeita."
        checked={suggestDeadlines}
        onChange={setSuggest}
      />
      <SettingRow
        id="unlinked"
        title="Notificar publicações sem processo"
        text="Avisa quando uma publicação não puder ser ligada a um processo cadastrado."
        checked={notifyUnlinked}
        onChange={setNotify}
      />

      <fieldset className="space-y-2">
        <legend className="text-sm font-semibold">Antecedência dos alertas de prazo</legend>
        <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="Antecedência dos alertas de prazo">
          {leadOptions.map((option) => (
            <label
              key={option.value}
              className="iu-lift flex min-h-11 cursor-pointer items-center gap-2 rounded-[10px] border-[1.5px] px-4 text-sm has-[:checked]:border-[var(--iu-brand)] has-[:checked]:bg-[var(--iu-brand-soft)]"
            >
              <input
                type="radio"
                name="lead"
                className="sr-only"
                checked={lead === option.value}
                onChange={() => setLead(option.value)}
              />
              {option.label}
            </label>
          ))}
        </div>
      </fieldset>

      <StepActions onBack={onBack} onSkip={onSkip} />
    </form>
  )
}

function SettingRow({
  id,
  title,
  text,
  checked,
  onChange,
}: {
  id: string
  title: string
  text: string
  checked: boolean
  onChange: (value: boolean) => void
}) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div>
        <label htmlFor={id} className="text-sm font-semibold">
          {title}
        </label>
        <p className="text-sm text-muted-foreground">{text}</p>
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onChange} />
    </div>
  )
}

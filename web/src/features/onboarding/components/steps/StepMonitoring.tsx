'use client'

import { X } from 'lucide-react'
import { useId, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { brazilianStates } from '@/lib/validation/common'
import { checkHours, tribunais } from '../../catalog'
import type { MonitoredPerson } from '../../model'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

const OAB = /^\d{3,7}$/

/** Passo 2: quem e o que a Athena monitora no DJEN/CNJ, e a que horas verifica. */
export function StepMonitoring({ state, user, onComplete, onSkip, onBack }: StepProps) {
  const initial = state.data.monitoring
  const signup = state.data.signup
  const [people, setPeople] = useState<MonitoredPerson[]>(
    initial?.people ??
      (signup?.oabNumber
        ? [{ name: user.name, oabNumber: signup.oabNumber.replace(/\D/g, ''), oabState: signup.oabState }]
        : [])
  )
  const [selected, setSelected] = useState<string[]>(initial?.tribunais ?? [])
  const [hour, setHour] = useState(initial?.hour ?? '06:00')
  const [filter, setFilter] = useState('')
  const [draft, setDraft] = useState({ name: '', oab: '', uf: '' })
  const [error, setError] = useState<string | null>(null)
  const filterId = useId()

  function addPerson() {
    const name = draft.name.trim()
    const oabNumber = draft.oab.replace(/\D/g, '')
    if (name.length < 3) return setError('Informe o nome completo, como aparece nas publicações.')
    if (!OAB.test(oabNumber)) return setError('Número da OAB inválido (3 a 7 dígitos).')
    if (!draft.uf) return setError('Selecione a UF da OAB.')
    if (people.some((p) => p.oabNumber === oabNumber && p.oabState === draft.uf)) {
      return setError('Esta OAB já está na lista.')
    }
    setPeople([...people, { name, oabNumber, oabState: draft.uf }])
    setDraft({ name: '', oab: '', uf: draft.uf })
    setError(null)
  }

  const toggle = (code: string) =>
    setSelected((current) => (current.includes(code) ? current.filter((c) => c !== code) : [...current, code]))

  const visible = tribunais.filter((code) => code.toLowerCase().includes(filter.trim().toLowerCase()))

  function submit(event: React.FormEvent) {
    event.preventDefault()
    if (people.length === 0) return setError('Adicione ao menos um nome com OAB para a Athena monitorar.')
    onComplete({ monitoring: { people, tribunais: selected, hour } })
  }

  return (
    <form onSubmit={submit} className="space-y-6" noValidate>
      <section aria-labelledby="mon-pessoas" className="space-y-3">
        <h3 id="mon-pessoas" className="text-sm font-semibold">
          Nomes e OABs
        </h3>
        {people.length > 0 ? (
          <ul className="flex flex-wrap gap-2" aria-label="Monitorados">
            {people.map((person) => (
              <li
                key={`${person.oabNumber}/${person.oabState}`}
                className="iu-in flex items-center gap-2 rounded-full border bg-card py-1 pr-1 pl-3 text-sm"
              >
                <span>
                  {person.name} · <span className="iu-mono">OAB/{person.oabState} {person.oabNumber}</span>
                </span>
                <button
                  type="button"
                  aria-label={`Remover ${person.name}`}
                  className="flex size-8 items-center justify-center rounded-full hover:bg-accent"
                  onClick={() => setPeople(people.filter((p) => p !== person))}
                >
                  <X className="size-4" aria-hidden />
                </button>
              </li>
            ))}
          </ul>
        ) : null}
        <div className="grid gap-2 sm:grid-cols-[1fr_8rem_6rem_auto]">
          <Input className="h-11" aria-label="Nome completo" placeholder="Nome completo" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
          <Input className="iu-mono h-11" aria-label="Número da OAB" inputMode="numeric" placeholder="OAB" value={draft.oab} onChange={(e) => setDraft({ ...draft, oab: e.target.value })} />
          <Select value={draft.uf} onValueChange={(uf) => setDraft({ ...draft, uf })}>
            <SelectTrigger className="h-11 w-full" aria-label="UF da OAB">
              <SelectValue placeholder="UF" />
            </SelectTrigger>
            <SelectContent>
              {brazilianStates.map((uf) => (
                <SelectItem key={uf} value={uf}>
                  {uf}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button type="button" variant="outline" className="h-11" onClick={addPerson}>
            Adicionar
          </Button>
        </div>
        {error ? (
          <p role="alert" className="iu-in text-sm text-[var(--iu-danger)]">
            {error}
          </p>
        ) : null}
      </section>

      <section aria-labelledby="mon-tribunais" className="space-y-3">
        <h3 id="mon-tribunais" className="text-sm font-semibold">
          Tribunais de interesse <span className="font-normal text-muted-foreground">({selected.length} selecionados)</span>
        </h3>
        <label htmlFor={filterId} className="sr-only">
          Buscar tribunal
        </label>
        <Input id={filterId} className="h-11" type="search" placeholder="Buscar (ex.: TJSP, TRT-2)" value={filter} onChange={(e) => setFilter(e.target.value)} />
        <ul className="grid max-h-48 grid-cols-2 gap-1 overflow-y-auto rounded-lg border p-2 sm:grid-cols-3">
          {visible.map((code) => (
            <li key={code}>
              <label className="flex min-h-11 cursor-pointer items-center gap-2 rounded-md px-2 text-sm hover:bg-accent">
                <Checkbox checked={selected.includes(code)} onCheckedChange={() => toggle(code)} />
                {code}
              </label>
            </li>
          ))}
          {visible.length === 0 ? <li className="col-span-full p-2 text-sm text-muted-foreground">Nenhum tribunal encontrado.</li> : null}
        </ul>
      </section>

      <section aria-labelledby="mon-hora" className="space-y-2">
        <h3 id="mon-hora" className="text-sm font-semibold">
          Horário da verificação diária
        </h3>
        <Select value={hour} onValueChange={setHour}>
          <SelectTrigger className="h-11 w-40" aria-labelledby="mon-hora">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {checkHours.map((h) => (
              <SelectItem key={h} value={h}>
                {h}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">
          A Athena verifica todos os dias e avisa o que exigir sua revisão.
        </p>
      </section>

      <StepActions onBack={onBack} onSkip={onSkip} />
    </form>
  )
}

'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { brazilianStates, oabNumberField } from '@/lib/validation/common'
import { tourByRole } from '../../catalog'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

const schema = z.object({
  displayName: z.string().trim().min(2, 'Informe como quer ser chamado(a).').max(80),
  oabNumber: oabNumberField,
  oabState: z.union([z.enum(brazilianStates), z.literal('')]),
})
type Values = z.infer<typeof schema>

/** Convidado, passo 1: nome de exibição e (só Advogado/Sócio) OAB. Foto: depende de storage de arquivos. */
export function StepProfile({ state, user, onComplete }: StepProps) {
  const needsOab = user.role === 'Lawyer' || user.role === 'Partner' || user.role === 'Owner'
  const profile = state.data.profile
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onBlur',
    defaultValues: {
      displayName: profile?.displayName ?? user.name,
      oabNumber: profile?.oabNumber ?? '',
      oabState: (profile?.oabState as Values['oabState']) ?? '',
    },
  })

  function submit(values: Values) {
    if (needsOab && (values.oabNumber || values.oabState) && !(values.oabNumber && values.oabState)) {
      form.setError(values.oabNumber ? 'oabState' : 'oabNumber', { message: 'Informe o número e a UF da OAB.' })
      return
    }
    onComplete({
      profile: {
        displayName: values.displayName,
        ...(needsOab && values.oabNumber ? { oabNumber: values.oabNumber.replace(/\D/g, ''), oabState: values.oabState } : {}),
      },
    })
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(submit)} className="space-y-4" noValidate>
        <FormField
          control={form.control}
          name="displayName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Como devemos te chamar?</FormLabel>
              <FormControl>
                <Input className="h-11" autoComplete="name" autoFocus {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        {needsOab ? (
          <div className="grid grid-cols-[1fr_7rem] gap-3">
            <FormField
              control={form.control}
              name="oabNumber"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>OAB (opcional)</FormLabel>
                  <FormControl>
                    <Input className="iu-mono h-11" inputMode="numeric" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="oabState"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>UF</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value}>
                    <FormControl>
                      <SelectTrigger className="h-11 w-full" aria-label="UF da OAB">
                        <SelectValue placeholder="UF" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {brazilianStates.map((uf) => (
                        <SelectItem key={uf} value={uf}>
                          {uf}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        ) : null}
        <StepActions />
      </form>
    </Form>
  )
}

/** Convidado, passo 2: 3–4 cartões só do que o perfil acessa. */
export function StepTour({ user, onComplete, onBack }: StepProps) {
  const cards = tourByRole[user.role ?? 'Lawyer'] ?? tourByRole.Lawyer!
  return (
    <div className="space-y-5">
      <ul className="iu-stagger grid gap-3 sm:grid-cols-2">
        {cards.map((card, index) => (
          <li key={card.title} style={{ ['--i' as string]: index }} className="iu-card iu-lift p-4">
            <p className="text-sm font-semibold">{card.title}</p>
            <p className="mt-1 text-sm text-muted-foreground">{card.text}</p>
          </li>
        ))}
      </ul>
      <StepActions onBack={onBack} onContinue={() => onComplete()} continueLabel="Ir para o painel" />
    </div>
  )
}

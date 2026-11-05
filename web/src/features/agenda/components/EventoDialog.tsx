'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { useAccess } from '@/hooks/useAccess'
import { toDateTimeLocal } from '../dates'
import { useCreateEvento, useDeleteEvento, useUpdateEvento } from '../hooks'
import {
  eventoFormDefaults,
  eventoFormSchema,
  type EventoFormValues,
  type EventoPreset,
} from '../schemas'
import { tipoEventoLabels, tiposEvento, type AgendaEvento } from '../types'
import { ProcessoSelect } from './ProcessoSelect'
import { ResponsavelSelect } from './ResponsavelSelect'

export interface EventoDialogState extends EventoPreset {
  /** Evento em edição; ausente = criação. */
  evento?: AgendaEvento
  /** Início sugerido na criação (clique numa célula). */
  inicio?: Date
}

interface EventoDialogProps {
  state: EventoDialogState | null
  onClose: () => void
}

/** Monta o diálogo só enquanto aberto, para o formulário nascer com os valores certos. */
export function EventoDialog({ state, onClose }: EventoDialogProps) {
  return (
    <Dialog open={state !== null} onOpenChange={(open) => (open ? undefined : onClose())}>
      {state ? <EventoDialogBody state={state} onClose={onClose} /> : null}
    </Dialog>
  )
}

function defaultsFor(state: EventoDialogState): EventoFormValues {
  const { evento } = state
  if (!evento) {
    const base = state.inicio ?? new Date()
    if (!state.inicio) base.setMinutes(0, 0, 0)

    return eventoFormDefaults(toDateTimeLocal(base), state)
  }

  return {
    titulo: evento.titulo,
    tipo: evento.tipo,
    diaInteiro: evento.diaInteiro,
    inicio: toDateTimeLocal(new Date(evento.inicio)),
    fim: evento.diaInteiro ? '' : toDateTimeLocal(new Date(evento.fim)),
    local: evento.local ?? '',
    descricao: evento.descricao ?? '',
    processoId: evento.processoId ?? '',
    responsavelId: evento.responsavelId ?? '',
  }
}

function EventoDialogBody({ state, onClose }: { state: EventoDialogState; onClose: () => void }) {
  const { evento } = state
  const [confirmDelete, setConfirmDelete] = useState(false)
  const { hasTeam } = useAccess()

  const create = useCreateEvento(onClose)
  const update = useUpdateEvento(evento?.id ?? '', onClose)
  const remove = useDeleteEvento(() => {
    setConfirmDelete(false)
    onClose()
  })

  const form = useForm<EventoFormValues>({
    resolver: zodResolver(eventoFormSchema),
    defaultValues: defaultsFor(state),
    mode: 'onBlur',
    reValidateMode: 'onChange',
  })

  const diaInteiro = useWatch({ control: form.control, name: 'diaInteiro' })
  const isSaving = create.isPending || update.isPending

  function submit(values: EventoFormValues) {
    if (evento) update.mutate(values)
    else create.mutate(values)
  }

  return (
    <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
      <DialogHeader>
        <DialogTitle>{evento ? 'Editar evento' : 'Novo evento'}</DialogTitle>
        <DialogDescription>
          Prazos, audiências e reuniões avisam o responsável 1 dia e 1 hora antes.
        </DialogDescription>
      </DialogHeader>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(submit)} className="space-y-4" noValidate>
          <FormField
            control={form.control}
            name="titulo"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Título</FormLabel>
                <FormControl>
                  <Input autoComplete="off" maxLength={200} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="tipo"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Tipo</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {tiposEvento.map((tipo) => (
                        <SelectItem key={tipo} value={tipo}>
                          {tipoEventoLabels[tipo]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="diaInteiro"
              render={({ field }) => (
                <FormItem className="flex flex-col justify-end gap-2">
                  <FormLabel>Dia inteiro</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="inicio"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Início</FormLabel>
                  <FormControl>
                    <Input
                      type={diaInteiro ? 'date' : 'datetime-local'}
                      {...field}
                      value={diaInteiro ? field.value.slice(0, 10) : field.value}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {diaInteiro ? null : (
              <FormField
                control={form.control}
                name="fim"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Término</FormLabel>
                    <FormControl>
                      <Input type="datetime-local" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
          </div>

          <FormField
            control={form.control}
            name="processoId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Processo</FormLabel>
                <ProcessoSelect value={field.value} onChange={field.onChange} />
                <FormMessage />
              </FormItem>
            )}
          />

          {hasTeam ? (
            <FormField
              control={form.control}
              name="responsavelId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Responsável</FormLabel>
                  <ResponsavelSelect
                    value={field.value}
                    onChange={field.onChange}
                    emptyLabel="Eu"
                    label="Responsável pelo evento"
                  />
                  <FormMessage />
                </FormItem>
              )}
            />
          ) : null}

          <FormField
            control={form.control}
            name="local"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Local</FormLabel>
                <FormControl>
                  <Input autoComplete="off" maxLength={300} placeholder="Opcional" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="descricao"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Descrição</FormLabel>
                <FormControl>
                  <Textarea rows={3} maxLength={4000} placeholder="Opcional" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <DialogFooter className="gap-2 sm:justify-between">
            {evento ? (
              <Button
                type="button"
                variant="outline"
                onClick={() => setConfirmDelete(true)}
                disabled={isSaving}
              >
                Excluir
              </Button>
            ) : (
              <span />
            )}
            <div className="flex gap-2">
              <Button type="button" variant="ghost" onClick={onClose} disabled={isSaving}>
                Cancelar
              </Button>
              <Button type="submit" disabled={isSaving}>
                {isSaving ? <LoadingSpinner /> : null}
                {evento ? 'Salvar' : 'Criar evento'}
              </Button>
            </div>
          </DialogFooter>
        </form>
      </Form>

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Excluir evento?"
        description="O evento sai da agenda e os lembretes deixam de ser enviados."
        confirmLabel="Excluir"
        destructive
        isPending={remove.isPending}
        onConfirm={() => evento && remove.mutate(evento.id)}
      />
    </DialogContent>
  )
}

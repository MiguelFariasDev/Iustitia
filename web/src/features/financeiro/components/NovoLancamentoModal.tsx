'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { CurrencyInput } from '@/components/shared/CurrencyInput'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
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
import { useCreateLancamento } from '../hooks'
import { lancamentoFormDefaults, lancamentoFormSchema, type LancamentoFormValues } from '../schemas'
import { tiposLancamento, tipoLancamentoLabels } from '../types'

/**
 * MOCK: grava no repositório em memória (ver mocks.ts) — o lançamento aparece na lista e nos
 * totais, mas some ao recarregar. TODO(financeiro-backend, Fase 4/8): sem mudança aqui; só
 * api.ts passa a chamar o servidor.
 */
export function NovoLancamentoModal() {
  const [open, setOpen] = useState(false)
  const { mutate, isPending } = useCreateLancamento()

  const form = useForm<LancamentoFormValues>({
    resolver: zodResolver(lancamentoFormSchema),
    defaultValues: lancamentoFormDefaults,
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  const tipo = useWatch({ control: form.control, name: 'tipo' })
  const rotuloPessoa = tipo === 'ContaPagar' ? 'Fornecedor' : 'Cliente'

  function handleOpenChange(next: boolean) {
    setOpen(next)
    if (!next) form.reset(lancamentoFormDefaults)
  }

  function onSubmit(values: LancamentoFormValues) {
    mutate(values, { onSuccess: () => handleOpenChange(false) })
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button>
          <Plus aria-hidden />
          Novo lançamento
        </Button>
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Novo lançamento</DialogTitle>
          <DialogDescription>
            Demonstração: o lançamento vale só até recarregar a página.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
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
                        {tiposLancamento.map((valor) => (
                          <SelectItem key={valor} value={valor}>
                            {tipoLancamentoLabels[valor]}
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
                name="status"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Situação</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="Pendente">Pendente</SelectItem>
                        <SelectItem value="Pago">Pago</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="descricao"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Descrição</FormLabel>
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
                name="pessoa"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{rotuloPessoa}</FormLabel>
                    <FormControl>
                      <Input autoComplete="off" maxLength={200} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="processo"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Processo (opcional)</FormLabel>
                    <FormControl>
                      <Input autoComplete="off" maxLength={30} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="valor"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Valor</FormLabel>
                    <FormControl>
                      <CurrencyInput
                        value={field.value}
                        onValueChange={field.onChange}
                        onBlur={field.onBlur}
                        name={field.name}
                        ref={field.ref}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="vencimento"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Vencimento</FormLabel>
                    <FormControl>
                      <Input type="date" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" disabled={isPending}>
                {isPending ? <LoadingSpinner decorative size="sm" /> : null}
                Registrar
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}

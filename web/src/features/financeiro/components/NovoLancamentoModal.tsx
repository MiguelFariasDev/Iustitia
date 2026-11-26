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
import { formatCurrency, formatDate } from '@/lib/format'
import { useCreateLancamento } from '../hooks'
import {
  lancamentoFormDefaults,
  lancamentoFormSchema,
  previaSerie,
  vencimentoDaParcela,
  type LancamentoFormValues,
} from '../schemas'
import {
  MAX_PARCELAS,
  recorrenciaLabels,
  recorrenciasLancamento,
  tiposLancamento,
  tipoLancamentoLabels,
} from '../types'

/**
 * Registra o lançamento no servidor: ele aparece na lista e nos totais para todo o escritório
 * autorizado (titular, sócios e financeiro).
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
  const recorrencia = useWatch({ control: form.control, name: 'recorrencia' })
  const parcelas = useWatch({ control: form.control, name: 'parcelas' })
  const valorInformado = useWatch({ control: form.control, name: 'valor' })
  const vencimentoInformado = useWatch({ control: form.control, name: 'vencimento' })
  const rotuloPessoa = tipo === 'ContaPagar' ? 'Fornecedor' : 'Cliente'
  const emSerie = recorrencia !== 'Unica'
  const rotuloValor =
    recorrencia === 'Parcelado' ? 'Valor total' : recorrencia === 'Mensal' ? 'Valor mensal' : 'Valor'
  const previa = previaSerie(recorrencia, valorInformado, parcelas)
  const ultimoVencimento =
    emSerie && vencimentoInformado && parcelas >= 2
      ? vencimentoDaParcela(vencimentoInformado, parcelas)
      : null

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
            O lançamento fica salvo no escritório, visível a quem tem acesso ao financeiro.
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
                        <SelectItem value="Pago">
                          {emSerie ? 'Primeira já paga' : 'Pago'}
                        </SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="recorrencia"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Forma de cobrança</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {recorrenciasLancamento.map((valor) => (
                          <SelectItem key={valor} value={valor}>
                            {recorrenciaLabels[valor]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {emSerie ? (
                <FormField
                  control={form.control}
                  name="parcelas"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>
                        {recorrencia === 'Mensal' ? 'Quantos meses' : 'Número de parcelas'}
                      </FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          inputMode="numeric"
                          min={2}
                          max={MAX_PARCELAS}
                          step={1}
                          name={field.name}
                          ref={field.ref}
                          onBlur={field.onBlur}
                          value={Number.isFinite(field.value) ? field.value : ''}
                          onChange={(event) => field.onChange(event.target.valueAsNumber)}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ) : null}
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
                    <FormLabel>{rotuloValor}</FormLabel>
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
                    <FormLabel>{emSerie ? 'Primeiro vencimento' : 'Vencimento'}</FormLabel>
                    <FormControl>
                      <Input type="date" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            {previa ? (
              <p role="status" className="rounded-lg border bg-muted/50 px-3 py-2 text-sm">
                {recorrencia === 'Mensal' ? (
                  <>
                    {parcelas} cobranças mensais de <strong>{formatCurrency(previa.valorParcela)}</strong>{' '}
                    (total {formatCurrency(previa.total)})
                  </>
                ) : (
                  <>
                    {parcelas}x de <strong>{formatCurrency(previa.valorParcela)}</strong>
                    {previa.valorUltima !== previa.valorParcela
                      ? ` (a última de ${formatCurrency(previa.valorUltima)})`
                      : ''}
                  </>
                )}
                {ultimoVencimento ? `, a última vencendo em ${formatDate(ultimoVencimento)}` : ''}
                . Cada {recorrencia === 'Mensal' ? 'mês' : 'parcela'} vira um lançamento próprio, que
                pode ser pago separadamente.
              </p>
            ) : null}

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

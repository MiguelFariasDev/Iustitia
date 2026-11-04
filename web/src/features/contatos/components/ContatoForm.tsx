'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import Link from 'next/link'
import { useForm } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
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
import { Textarea } from '@/components/ui/textarea'
import { formatTelefone } from '@/lib/validation/documento'
import { formatCpfOuCnpj } from '../documento'
import { contatoFormDefaults, contatoFormSchema, type ContatoFormValues } from '../schemas'
import { tiposContato, tipoContatoLabels } from '../types'

interface ContatoFormProps {
  defaultValues?: ContatoFormValues
  submitLabel: string
  isSubmitting: boolean
  onSubmit: (values: ContatoFormValues) => void
  cancelHref: string
}

export function ContatoForm({
  defaultValues = contatoFormDefaults,
  submitLabel,
  isSubmitting,
  onSubmit,
  cancelHref,
}: ContatoFormProps) {
  const form = useForm<ContatoFormValues>({
    resolver: zodResolver(contatoFormSchema),
    defaultValues,
    // Valida ao sair do campo e revalida a cada tecla depois do primeiro erro.
    mode: 'onBlur',
    reValidateMode: 'onChange',
  })

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6" noValidate>
        <Card>
          <CardContent className="grid gap-4 pt-6 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="nome"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>Nome</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" maxLength={200} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

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
                      {tiposContato.map((valor) => (
                        <SelectItem key={valor} value={valor}>
                          {tipoContatoLabels[valor]}
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
              name="documento"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>CPF ou CNPJ</FormLabel>
                  <FormControl>
                    <Input
                      inputMode="numeric"
                      autoComplete="off"
                      spellCheck={false}
                      placeholder="Opcional"
                      maxLength={18}
                      {...field}
                      onChange={(event) => field.onChange(formatCpfOuCnpj(event.target.value))}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>E-mail</FormLabel>
                  <FormControl>
                    <Input type="email" autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="telefone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Telefone</FormLabel>
                  <FormControl>
                    <Input
                      type="tel"
                      inputMode="tel"
                      autoComplete="off"
                      placeholder="(00) 00000-0000"
                      maxLength={15}
                      {...field}
                      onChange={(event) => field.onChange(formatTelefone(event.target.value))}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="observacoes"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>Observações</FormLabel>
                  <FormControl>
                    <Textarea rows={4} maxLength={2000} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" asChild>
            <Link href={cancelHref as never}>Cancelar</Link>
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? <LoadingSpinner decorative size="sm" /> : null}
            {submitLabel}
          </Button>
        </div>
      </form>
    </Form>
  )
}

'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import Link from 'next/link'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Form,
  FormControl,
  FormDescription,
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
import { formatCep, formatTelefone } from '@/lib/validation/documento'
import { clienteFormDefaults, clienteFormSchema, type ClienteFormValues } from '../schemas'
import { tipoClienteLabels } from '../types'
import { DocumentoInput } from './DocumentoInput'

interface ClienteFormProps {
  defaultValues?: ClienteFormValues
  /** Edição não permite trocar tipo nem documento — são a identidade do cliente. */
  mode: 'create' | 'edit'
  submitLabel: string
  isSubmitting: boolean
  onSubmit: (values: ClienteFormValues) => void
  cancelHref: string
}

export function ClienteForm({
  defaultValues = clienteFormDefaults,
  mode,
  submitLabel,
  isSubmitting,
  onSubmit,
  cancelHref,
}: ClienteFormProps) {
  const form = useForm<ClienteFormValues>({
    resolver: zodResolver(clienteFormSchema),
    defaultValues,
    // Valida ao sair do campo e revalida a cada tecla depois do primeiro erro: avisa cedo
    // sem acusar "CPF inválido" no terceiro dígito digitado.
    mode: 'onBlur',
    reValidateMode: 'onChange',
  })

  const tipo = form.watch('tipo')
  const isPessoaFisica = tipo === 'PessoaFisica'

  // Trocar o tipo invalida o documento já digitado (CPF e CNPJ têm tamanhos diferentes) e
  // a data de nascimento deixa de existir para PJ. Limpar é melhor que deixar um valor que
  // o backend vai recusar.
  useEffect(() => {
    if (mode !== 'create') return

    form.setValue('documento', '')
    if (!isPessoaFisica) form.setValue('dataNascimento', '')
  }, [form, isPessoaFisica, mode])

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6" noValidate>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Identificação</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="tipo"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Tipo</FormLabel>
                  <Select
                    value={field.value}
                    onValueChange={field.onChange}
                    disabled={mode === 'edit'}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value="PessoaFisica">{tipoClienteLabels.PessoaFisica}</SelectItem>
                      <SelectItem value="PessoaJuridica">
                        {tipoClienteLabels.PessoaJuridica}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                  {mode === 'edit' ? (
                    <FormDescription>
                      Tipo e documento não podem ser alterados. Para corrigi-los, cadastre um novo
                      cliente.
                    </FormDescription>
                  ) : null}
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="documento"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{isPessoaFisica ? 'CPF' : 'CNPJ'}</FormLabel>
                  <FormControl>
                    <DocumentoInput
                      tipo={tipo}
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      name={field.name}
                      ref={field.ref}
                      disabled={mode === 'edit'}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="nome"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>{isPessoaFisica ? 'Nome completo' : 'Razão social'}</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" maxLength={200} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {isPessoaFisica ? (
              <FormField
                control={form.control}
                name="dataNascimento"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Data de nascimento</FormLabel>
                    <FormControl>
                      <Input type="date" max={today()} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Contato</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>E-mail</FormLabel>
                  <FormControl>
                    <Input
                      type="email"
                      inputMode="email"
                      autoComplete="off"
                      spellCheck={false}
                      {...field}
                    />
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
                      inputMode="tel"
                      autoComplete="off"
                      placeholder="(00) 00000-0000"
                      maxLength={15}
                      value={field.value ?? ''}
                      onBlur={field.onBlur}
                      name={field.name}
                      ref={field.ref}
                      onChange={(event) => field.onChange(formatTelefone(event.target.value))}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Endereço</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-6">
            <FormField
              control={form.control}
              name="cep"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>CEP</FormLabel>
                  <FormControl>
                    <Input
                      inputMode="numeric"
                      autoComplete="postal-code"
                      spellCheck={false}
                      placeholder="00000-000"
                      maxLength={9}
                      value={field.value ?? ''}
                      onBlur={field.onBlur}
                      name={field.name}
                      ref={field.ref}
                      onChange={(event) => field.onChange(formatCep(event.target.value))}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="logradouro"
              render={({ field }) => (
                <FormItem className="sm:col-span-3">
                  <FormLabel>Logradouro</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="numero"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Número</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="complemento"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>Complemento</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="bairro"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>Bairro</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="cidade"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Cidade</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="uf"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>UF</FormLabel>
                  <FormControl>
                    <Input
                      autoComplete="off"
                      spellCheck={false}
                      maxLength={2}
                      className="uppercase"
                      value={field.value ?? ''}
                      onBlur={field.onBlur}
                      name={field.name}
                      ref={field.ref}
                      onChange={(event) => field.onChange(event.target.value.toUpperCase())}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Observações</CardTitle>
          </CardHeader>
          <CardContent>
            <FormField
              control={form.control}
              name="observacoes"
              render={({ field }) => (
                <FormItem>
                  <FormLabel className="sr-only">Observações</FormLabel>
                  <FormControl>
                    <Textarea
                      rows={4}
                      maxLength={2000}
                      placeholder="Anotações internas do escritório sobre este cliente…"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
            {isSubmitting ? <LoadingSpinner size="sm" decorative /> : null}
            {submitLabel}
          </Button>
          <Button variant="ghost" asChild>
            <Link href={cancelHref as never}>Cancelar</Link>
          </Button>
        </div>
      </form>
    </Form>
  )
}

/** Teto do seletor de data: ninguém nasceu amanhã. */
function today() {
  return new Date().toISOString().slice(0, 10)
}

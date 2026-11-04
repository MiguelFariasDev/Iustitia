'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, Search } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
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
import {
  formatCnj,
  getTribunalFromCnj,
  isValidCnj,
  tribunalTemConsultaPublica,
} from '@/lib/validation/documento'
import { toFormPatch, type ProcessoTribunal } from '../datajud'
import { useConsultarTribunal } from '../hooks'
import { processoFormDefaults, processoFormSchema, type ProcessoFormValues } from '../schemas'
import { areaDireitoLabels, areasDireito } from '../types'
import { ClienteSelect } from './ClienteSelect'
import { ConsultaTribunalDialog } from './ConsultaTribunalDialog'

interface ProcessoFormProps {
  defaultValues?: ProcessoFormValues
  /** Na edição o número CNJ é imutável — é a chave das publicações já vinculadas. */
  mode: 'create' | 'edit'
  submitLabel: string
  isSubmitting: boolean
  onSubmit: (values: ProcessoFormValues) => void
  cancelHref: string
}

export function ProcessoForm({
  defaultValues = processoFormDefaults,
  mode,
  submitLabel,
  isSubmitting,
  onSubmit,
  cancelHref,
}: ProcessoFormProps) {
  const form = useForm<ProcessoFormValues>({
    resolver: zodResolver(processoFormSchema),
    defaultValues,
    mode: 'onBlur',
    reValidateMode: 'onChange',
  })

  const [consultaAberta, setConsultaAberta] = useState(false)
  const consulta = useConsultarTribunal()

  const cnjNumber = form.watch('cnjNumber')
  const tribunal = getTribunalFromCnj(cnjNumber)

  // Três condições, e todas importam: número completo e com dígito verificador correto
  // (senão a consulta é certeza de "não encontrado"), e tribunal atendido pelo DataJud —
  // o STF, por exemplo, tem número válido e não publica lá.
  const podeConsultar =
    mode === 'create' && isValidCnj(cnjNumber) && tribunalTemConsultaPublica(tribunal)

  const tituloBotaoConsulta = !isValidCnj(cnjNumber)
    ? 'Informe um número CNJ válido para consultar'
    : !tribunalTemConsultaPublica(tribunal)
      ? `${tribunal ?? 'Este tribunal'} não é atendido pela consulta pública do CNJ`
      : `Consultar no ${tribunal}`

  function consultar() {
    setConsultaAberta(true)
    consulta.mutate(cnjNumber)
  }

  function aplicarDados(processo: ProcessoTribunal) {
    // shouldDirty para o formulário saber que há alterações não salvas; shouldValidate para
    // um campo preenchido pela consulta ser validado como qualquer outro.
    for (const [campo, valor] of Object.entries(toFormPatch(processo))) {
      form.setValue(campo as keyof ProcessoFormValues, valor as string, {
        shouldDirty: true,
        shouldValidate: true,
      })
    }
  }

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
              name="cnjNumber"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Número do processo (CNJ)</FormLabel>
                  <div className="flex gap-2">
                    <FormControl>
                      <Input
                        inputMode="numeric"
                        autoComplete="off"
                        spellCheck={false}
                        placeholder="0000000-00.0000.0.00.0000"
                        maxLength={25}
                        disabled={mode === 'edit'}
                        value={field.value}
                        onBlur={field.onBlur}
                        name={field.name}
                        ref={field.ref}
                        onChange={(event) => field.onChange(formatCnj(event.target.value))}
                      />
                    </FormControl>
                    {mode === 'create' ? (
                      <Button
                        type="button"
                        variant="outline"
                        size="icon"
                        onClick={consultar}
                        disabled={!podeConsultar || consulta.isPending}
                        aria-label={
                          tribunal
                            ? `Consultar este processo no ${tribunal}`
                            : 'Consultar processo no tribunal'
                        }
                        title={tituloBotaoConsulta}
                      >
                        {consulta.isPending ? (
                          <Loader2 className="animate-spin" aria-hidden />
                        ) : (
                          <Search aria-hidden />
                        )}
                      </Button>
                    ) : null}
                  </div>
                  <FormDescription>
                    {mode === 'edit'
                      ? 'O número não pode ser alterado: é por ele que as publicações já capturadas estão vinculadas.'
                      : podeConsultar
                        ? `Consulte no ${tribunal} para preencher os dados automaticamente. Ao cadastrar, o sistema passa a buscar as publicações deste processo no diário.`
                        : 'Ao cadastrar, o sistema passa a buscar as publicações deste processo no diário.'}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="clienteId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Cliente</FormLabel>
                  <FormControl>
                    <ClienteSelect value={field.value} onChange={field.onChange} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="area"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Área do direito</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {areasDireito.map((area) => (
                        <SelectItem key={area} value={area}>
                          {areaDireitoLabels[area]}
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
              name="dataDistribuicao"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Data de distribuição</FormLabel>
                  <FormControl>
                    <Input type="date" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Foro</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-3">
            <FormField
              control={form.control}
              name="tribunal"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Tribunal</FormLabel>
                  <FormControl>
                    <Input
                      autoComplete="off"
                      spellCheck={false}
                      placeholder="TJSP"
                      maxLength={20}
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

            <FormField
              control={form.control}
              name="comarca"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Comarca</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="vara"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Vara</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Detalhes do caso</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="assunto"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>Assunto</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" maxLength={300} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="parteContraria"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Parte contrária</FormLabel>
                  <FormControl>
                    <Input autoComplete="off" maxLength={200} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="valorCausa"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Valor da causa</FormLabel>
                  <FormControl>
                    <Input inputMode="decimal" autoComplete="off" placeholder="0,00" {...field} />
                  </FormControl>
                  <FormDescription>Use vírgula para os centavos.</FormDescription>
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

      {mode === 'create' ? (
        <ConsultaTribunalDialog
          open={consultaAberta}
          onOpenChange={setConsultaAberta}
          isPending={consulta.isPending}
          result={consulta.data}
          error={consulta.error}
          tribunal={tribunal}
          onUsarDados={aplicarDados}
        />
      ) : null}
    </Form>
  )
}

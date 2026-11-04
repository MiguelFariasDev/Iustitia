'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { Upload } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
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
import { ACCEPT_UPLOAD } from '../arquivo'
import { useUploadDocumento } from '../hooks'
import { uploadFormSchema, type UploadFormValues } from '../schemas'
import { tiposVinculo, tipoVinculoLabels } from '../types'

const defaults = {
  file: undefined,
  vinculoTipo: 'Avulso',
  vinculoRotulo: '',
} as unknown as UploadFormValues

/** Botão "Upload" + o diálogo. O gatilho mora aqui para o cabeçalho da página ficar no servidor. */
export function DocumentoUploadModal() {
  const [open, setOpen] = useState(false)
  const { mutate, isPending } = useUploadDocumento()

  const form = useForm<UploadFormValues>({
    resolver: zodResolver(uploadFormSchema),
    defaultValues: defaults,
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  const vinculoTipo = useWatch({ control: form.control, name: 'vinculoTipo' })

  function handleOpenChange(next: boolean) {
    setOpen(next)
    // Fechar descarta o rascunho: reabrir com o arquivo anterior selecionado confundiria.
    if (!next) form.reset(defaults)
  }

  function onSubmit(values: UploadFormValues) {
    mutate(values, { onSuccess: () => handleOpenChange(false) })
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button>
          <Upload aria-hidden />
          Upload
        </Button>
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Enviar documento</DialogTitle>
          <DialogDescription>PDF, DOCX, PNG ou JPG de até 10 MB.</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
            <FormField
              control={form.control}
              name="file"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Arquivo</FormLabel>
                  <FormControl>
                    <Input
                      type="file"
                      accept={ACCEPT_UPLOAD}
                      name={field.name}
                      ref={field.ref}
                      onBlur={field.onBlur}
                      // `File` não é controlável: o input guarda o arquivo e o formulário o espelha.
                      onChange={(event) => field.onChange(event.target.files?.[0])}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="vinculoTipo"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Vínculo</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {tiposVinculo.map((valor) => (
                        <SelectItem key={valor} value={valor}>
                          {tipoVinculoLabels[valor]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            {vinculoTipo !== 'Avulso' ? (
              <FormField
                control={form.control}
                name="vinculoRotulo"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>
                      {vinculoTipo === 'Processo' ? 'Número do processo' : 'Nome do cliente'}
                    </FormLabel>
                    <FormControl>
                      <Input autoComplete="off" maxLength={200} {...field} />
                    </FormControl>
                    <FormDescription>
                      Texto livre por enquanto: a busca no cadastro chega com o servidor.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ) : null}

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" disabled={isPending}>
                {isPending ? <LoadingSpinner decorative size="sm" /> : null}
                Enviar
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}

'use client'

import { Loader2, Search } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { routes } from '@/config/routes'
import { ApiError } from '@/lib/api/errors'
import {
  formatCnj,
  getTribunalFromCnj,
  isValidCnj,
  tribunalTemConsultaPublica,
} from '@/lib/validation/documento'
import { toFormPatch, type ProcessoTribunal } from '../datajud'
import { useConsultarTribunal, useCreateProcesso } from '../hooks'
import { processoFormDefaults, type ProcessoFormValues } from '../schemas'
import { ProcessoEncontrado } from './ConsultaTribunalDialog'
import { ProcessoForm } from './ProcessoForm'

/**
 * Cadastro por número: a pessoa informa só o CNJ, o sistema consulta o tribunal (DataJud) e
 * abre o formulário já preenchido. Se o processo não é encontrado, o tribunal não é atendido
 * ou o serviço está fora, o caminho manual segue aberto com o número já digitado.
 */
export function NovoProcessoForm() {
  const { mutate, isPending } = useCreateProcesso()
  const consulta = useConsultarTribunal()

  const [numero, setNumero] = useState('')
  // Nulo = ainda na etapa do número. Preenchido = formulário aberto (com ou sem dados).
  const [valoresIniciais, setValoresIniciais] = useState<ProcessoFormValues | null>(null)
  const [encontrado, setEncontrado] = useState<ProcessoTribunal | null>(null)

  const tribunal = getTribunalFromCnj(numero)
  const valido = isValidCnj(numero)
  const atendido = tribunalTemConsultaPublica(tribunal)

  function abrirFormulario(processo: ProcessoTribunal | null) {
    setEncontrado(processo)
    setValoresIniciais({
      ...processoFormDefaults,
      cnjNumber: numero,
      ...(processo ? toFormPatch(processo) : {}),
    })
  }

  function buscar() {
    consulta.mutate(numero, {
      onSuccess: (resultado) => {
        if (resultado.found && resultado.processo) abrirFormulario(resultado.processo)
      },
    })
  }

  function trocarNumero() {
    setValoresIniciais(null)
    setEncontrado(null)
    consulta.reset()
  }

  if (valoresIniciais) {
    return (
      <div className="space-y-6">
        {encontrado ? (
          <Card>
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">
                Dados encontrados no {encontrado.tribunal}
              </CardTitle>
              <Button type="button" variant="ghost" size="sm" onClick={trocarNumero}>
                Trocar número
              </Button>
            </CardHeader>
            <CardContent>
              <ProcessoEncontrado processo={encontrado} />
            </CardContent>
          </Card>
        ) : (
          <p className="text-sm text-muted-foreground">
            Preenchimento manual.{' '}
            <button type="button" className="underline" onClick={trocarNumero}>
              Buscar por outro número
            </button>
          </p>
        )}

        <ProcessoForm
          // Remonta o formulário quando os dados iniciais mudam (nova consulta).
          key={valoresIniciais.cnjNumber + (encontrado ? ':d' : ':m')}
          mode="create"
          defaultValues={valoresIniciais}
          submitLabel="Cadastrar processo"
          isSubmitting={isPending}
          onSubmit={(values) => mutate(values)}
          cancelHref={routes.processes}
        />
      </div>
    )
  }

  const naoEncontrado = consulta.data && !consulta.data.found
  const erro = consulta.error
  const mensagemErro =
    erro instanceof ApiError ? erro.message : 'Não foi possível consultar o tribunal agora.'

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Buscar processo pelo número</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <form
          className="space-y-2"
          onSubmit={(event) => {
            event.preventDefault()
            if (valido && atendido) buscar()
          }}
        >
          <Label htmlFor="cnj-busca">Número do processo (CNJ)</Label>
          <div className="flex gap-2">
            <Input
              id="cnj-busca"
              inputMode="numeric"
              autoComplete="off"
              spellCheck={false}
              autoFocus
              placeholder="0000000-00.0000.0.00.0000"
              maxLength={25}
              value={numero}
              onChange={(event) => {
                setNumero(formatCnj(event.target.value))
                if (consulta.data || consulta.error) consulta.reset()
              }}
            />
            <Button type="submit" disabled={!valido || !atendido || consulta.isPending}>
              {consulta.isPending ? (
                <Loader2 className="animate-spin" aria-hidden />
              ) : (
                <Search aria-hidden />
              )}
              Buscar
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">
            {!valido
              ? 'Digite os 20 dígitos. Tribunais do Piauí e do Ceará (TJPI, TJCE, TRF5, TRT22, TRT7) e demais tribunais do DataJud são consultados automaticamente.'
              : !atendido
                ? `${tribunal ?? 'Este tribunal'} não é atendido pela consulta pública do CNJ — preencha manualmente.`
                : `Será consultado no ${tribunal}.`}
          </p>
        </form>

        {naoEncontrado ? (
          <p role="status" className="text-sm text-muted-foreground">
            Processo não encontrado no {consulta.data?.tribunal ?? tribunal}. Nem todo processo está
            na base pública do CNJ.
          </p>
        ) : null}
        {erro ? (
          <p role="alert" className="text-sm text-destructive">
            {mensagemErro}
          </p>
        ) : null}

        <div className="flex flex-wrap items-center gap-3">
          <Button
            type="button"
            variant="outline"
            disabled={!valido}
            onClick={() => abrirFormulario(null)}
          >
            Preencher manualmente
          </Button>
          <Button variant="ghost" asChild>
            <Link href={routes.processes as never}>Cancelar</Link>
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

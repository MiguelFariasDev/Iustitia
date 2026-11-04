'use client'

import { useQuery } from '@tanstack/react-query'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { routes } from '@/config/routes'
import { useUpdateProcesso } from '../hooks'
import { processoQuery } from '../queries'
import type { ProcessoFormValues } from '../schemas'
import type { Processo } from '../types'
import { ProcessoForm } from './ProcessoForm'

export function EditarProcessoForm({ processoId }: { processoId: string }) {
  const { data: processo, isPending } = useQuery(processoQuery(processoId))

  // O responsável não é editável nesta tela (a troca de responsável é uma ação própria, que
  // chega com a gestão de equipe) — mas o backend exige o campo no update, então o valor
  // atual é repassado em vez de ser perdido.
  const { mutate, isPending: isSubmitting } = useUpdateProcesso(
    processoId,
    processo?.responsavelUserId ?? ''
  )

  if (isPending || !processo) {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Carregando processo…" />
      </div>
    )
  }

  return (
    <ProcessoForm
      mode="edit"
      defaultValues={toFormValues(processo)}
      submitLabel="Salvar alterações"
      isSubmitting={isSubmitting}
      onSubmit={(values) => mutate(values)}
      cancelHref={`${routes.processes}/${processoId}`}
    />
  )
}

/** Campo nulo vira string vazia: os inputs são controlados (ver EditarClienteForm). */
function toFormValues(processo: Processo): ProcessoFormValues {
  return {
    cnjNumber: processo.cnjNumber,
    clienteId: processo.clienteId,
    area: processo.area,
    tribunal: processo.tribunal ?? '',
    comarca: processo.comarca ?? '',
    vara: processo.vara ?? '',
    assunto: processo.assunto ?? '',
    parteContraria: processo.parteContraria ?? '',
    // De volta para o formato pt-BR que o campo aceita e o schema sabe parsear.
    valorCausa:
      processo.valorCausa === null ? '' : processo.valorCausa.toFixed(2).replace('.', ','),
    dataDistribuicao: processo.dataDistribuicao?.slice(0, 10) ?? '',
    observacoes: processo.observacoes ?? '',
  }
}

'use client'

import { FolderOpen } from 'lucide-react'
import { useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DemoNotice } from '@/components/shared/DemoNotice'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { SearchInput } from '@/components/shared/SearchInput'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { baixarDocumento } from '../api'
import { useDeleteDocumento, useDocumentos } from '../hooks'
import {
  tiposArquivo,
  tiposVinculo,
  tipoVinculoLabels,
  type Documento,
  type DocumentosFilters,
  type TipoArquivo,
  type TipoVinculo,
} from '../types'
import { DocumentoGrid } from './DocumentoGrid'

const TODOS = '__todos__'

export function DocumentosView() {
  const [filters, setFilters] = useState<DocumentosFilters>({})
  const [aExcluir, setAExcluir] = useState<Documento | null>(null)

  const { data, isPending, isError, error, refetch } = useDocumentos(filters)
  const { mutate: excluir, isPending: excluindo } = useDeleteDocumento()

  const temFiltro = Boolean(filters.busca || filters.tipoArquivo || filters.vinculo)

  return (
    <div className="space-y-4">
      <DemoNotice modulo="Documentos">
        os arquivos enviados ficam só na memória desta aba e somem ao recarregar.
      </DemoNotice>

      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <SearchInput
          onSearch={(busca) => setFilters((f) => ({ ...f, busca }))}
          label="Pesquisar documentos"
          placeholder="Nome do arquivo…"
          className="lg:max-w-sm"
        />

        <div className="flex flex-wrap gap-x-6 gap-y-2">
          <SegmentedControl
            label="Filtrar por tipo de arquivo"
            value={filters.tipoArquivo ?? TODOS}
            onChange={(valor) =>
              setFilters((f) => ({
                ...f,
                tipoArquivo: valor === TODOS ? null : (valor as TipoArquivo),
              }))
            }
            options={[
              { value: TODOS, label: 'Todos' },
              ...tiposArquivo.map((tipo) => ({ value: tipo, label: tipo })),
            ]}
          />
          <SegmentedControl
            label="Filtrar por vínculo"
            value={filters.vinculo ?? TODOS}
            onChange={(valor) =>
              setFilters((f) => ({
                ...f,
                vinculo: valor === TODOS ? null : (valor as TipoVinculo),
              }))
            }
            options={[
              { value: TODOS, label: 'Todos' },
              ...tiposVinculo.map((vinculo) => ({
                value: vinculo,
                label: tipoVinculoLabels[vinculo],
              })),
            ]}
          />
        </div>
      </div>

      {isError ? (
        <ErrorState
          error={error}
          title="Não foi possível carregar os documentos"
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <div className="flex justify-center py-12">
          <LoadingSpinner label="Carregando documentos…" />
        </div>
      ) : data.length === 0 ? (
        <EmptyState
          icon={FolderOpen}
          title={temFiltro ? 'Nenhum documento encontrado' : 'Nenhum documento enviado'}
          description={
            temFiltro
              ? 'Tente outro nome ou remova os filtros.'
              : 'Use o botão Upload para enviar o primeiro arquivo.'
          }
        />
      ) : (
        <DocumentoGrid documentos={data} onDownload={baixarDocumento} onDelete={setAExcluir} />
      )}

      <ConfirmDialog
        open={aExcluir !== null}
        onOpenChange={(open) => {
          if (!open) setAExcluir(null)
        }}
        title="Excluir documento?"
        description={`${aExcluir?.nome ?? 'O documento'} será removido. Esta ação não pode ser desfeita.`}
        confirmLabel="Excluir"
        destructive
        isPending={excluindo}
        onConfirm={() => {
          if (!aExcluir) return
          excluir(aExcluir.id, { onSettled: () => setAExcluir(null) })
        }}
      />
    </div>
  )
}

'use client'

import { FolderOpen } from 'lucide-react'
import { useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DemoNotice } from '@/components/shared/DemoNotice'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { FilterChip } from '@/components/shared/FilterChip'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { SearchInput } from '@/components/shared/SearchInput'
import { baixarDocumento } from '../api'
import { useDeleteDocumento, useDocumentos } from '../hooks'
import {
  tiposArquivo,
  tiposVinculo,
  tipoVinculoLabels,
  type Documento,
  type DocumentosFilters,
} from '../types'
import { DocumentoGrid } from './DocumentoGrid'

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
          <div
            className="flex flex-wrap gap-2"
            role="group"
            aria-label="Filtrar por tipo de arquivo"
          >
            {tiposArquivo.map((tipo) => (
              <FilterChip
                key={tipo}
                active={filters.tipoArquivo === tipo}
                onClick={() =>
                  setFilters((f) => ({ ...f, tipoArquivo: f.tipoArquivo === tipo ? null : tipo }))
                }
              >
                {tipo}
              </FilterChip>
            ))}
          </div>
          <div className="flex flex-wrap gap-2" role="group" aria-label="Filtrar por vínculo">
            {tiposVinculo.map((vinculo) => (
              <FilterChip
                key={vinculo}
                active={filters.vinculo === vinculo}
                onClick={() =>
                  setFilters((f) => ({ ...f, vinculo: f.vinculo === vinculo ? null : vinculo }))
                }
              >
                {tipoVinculoLabels[vinculo]}
              </FilterChip>
            ))}
          </div>
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

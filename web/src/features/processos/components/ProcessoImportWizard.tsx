'use client'

import { Download, FileUp, Loader2 } from 'lucide-react'
import Link from 'next/link'
import { useRef, useState } from 'react'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { routes } from '@/config/routes'
import { ApiError } from '@/lib/api/errors'
import { useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { onlyDigits } from '@/lib/validation/documento'
import { listClientes } from '../../clientes/api'
import { createProcesso } from '../api'
import { analisarPlanilha, modeloCsv, parseCsv, type LinhaImportacao } from '../importacao'

type Passo = 'upload' | 'preview' | 'relatorio'

interface LinhaResolvida extends LinhaImportacao {
  clienteId: string | null
}

interface Falha {
  linha: number
  cnj: string
  motivo: string
}

interface Relatorio {
  importados: number
  duplicados: number
  falhas: Falha[]
}

/** Consulta um cliente por documento; ativo e com documento idêntico, senão nulo. */
async function resolverClientes(linhas: LinhaImportacao[]) {
  const documentos = [...new Set(linhas.map((l) => l.documentoCliente).filter(Boolean))]
  const mapa = new Map<string, string>()

  for (const documento of documentos) {
    const pagina = await listClientes({ busca: documento, pageSize: 5 })
    const achado = pagina.items.find((c) => onlyDigits(c.documento) === documento && c.isActive)
    if (achado) mapa.set(documento, achado.id)
  }

  return mapa
}

export function ProcessoImportWizard() {
  const queryClient = useQueryClient()
  const [passo, setPasso] = useState<Passo>('upload')
  const [linhas, setLinhas] = useState<LinhaResolvida[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [lendo, setLendo] = useState(false)
  const [progresso, setProgresso] = useState(0)
  const [relatorio, setRelatorio] = useState<Relatorio | null>(null)
  const input = useRef<HTMLInputElement>(null)

  function baixarModelo() {
    const url = URL.createObjectURL(new Blob(['﻿' + modeloCsv], { type: 'text/csv' }))
    const a = document.createElement('a')
    a.href = url
    a.download = 'modelo-importacao-processos.csv'
    a.click()
    URL.revokeObjectURL(url)
  }

  async function lerArquivo(arquivo: File) {
    setErro(null)
    if (!arquivo.name.toLowerCase().endsWith('.csv')) {
      setErro('Envie um arquivo .csv. No Excel: Arquivo → Salvar como → CSV.')
      return
    }
    setLendo(true)
    try {
      const { linhas: analisadas, erroCabecalho } = analisarPlanilha(parseCsv(await arquivo.text()))
      if (erroCabecalho) return setErro(erroCabecalho)
      if (analisadas.length === 0) return setErro('A planilha não tem linhas de dados.')
      if (analisadas.length > 500) return setErro('Limite de 500 processos por importação.')

      const clientes = await resolverClientes(analisadas)
      setLinhas(
        analisadas.map((l) => {
          const clienteId = clientes.get(l.documentoCliente) ?? null
          const erros = [...l.erros]
          if (!clienteId && l.documentoCliente)
            erros.push('Cliente não encontrado (cadastre-o antes).')
          return { ...l, erros, clienteId }
        })
      )
      setPasso('preview')
    } catch {
      setErro('Não foi possível ler a planilha. Verifique o arquivo e tente de novo.')
    } finally {
      setLendo(false)
    }
  }

  const importaveis = linhas.filter((l) => l.erros.length === 0 && !l.duplicadaNoArquivo)
  const comErro = linhas.filter((l) => l.erros.length > 0)
  const repetidas = linhas.filter((l) => l.duplicadaNoArquivo && l.erros.length === 0)

  async function importar() {
    setPasso('relatorio')
    setProgresso(0)
    const resultado: Relatorio = { importados: 0, duplicados: repetidas.length, falhas: [] }

    // Sequencial de propósito: cada cadastro liga a captura de publicações e passa pelo
    // rate limit da API; paralelizar 500 POSTs só produziria 429.
    for (const [i, l] of importaveis.entries()) {
      try {
        await createProcesso({ ...l.valores, clienteId: l.clienteId! })
        resultado.importados++
      } catch (e) {
        if (e instanceof ApiError && e.code === 'PROCESS_CNJ_DUPLICATED') resultado.duplicados++
        else
          resultado.falhas.push({
            linha: l.linha,
            cnj: l.valores.cnjNumber,
            motivo: e instanceof ApiError ? e.message : 'Erro inesperado.',
          })
      }
      setProgresso(Math.round(((i + 1) / importaveis.length) * 100))
    }

    void queryClient.invalidateQueries({ queryKey: queryKeys.processos.all })
    setRelatorio(resultado)
  }

  if (passo === 'upload') {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">1. Envie a planilha</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault()
              const arquivo = e.dataTransfer.files[0]
              if (arquivo) void lerArquivo(arquivo)
            }}
            className="flex flex-col items-center gap-3 rounded-xl border border-dashed p-10 text-center"
          >
            {lendo ? (
              <Loader2 className="size-6 animate-spin" aria-hidden />
            ) : (
              <FileUp className="size-6" aria-hidden />
            )}
            <p className="text-sm">Arraste o arquivo .csv aqui ou</p>
            <Button
              type="button"
              variant="outline"
              disabled={lendo}
              onClick={() => input.current?.click()}
            >
              Escolher arquivo
            </Button>
            <input
              ref={input}
              type="file"
              accept=".csv,text/csv"
              className="sr-only"
              aria-label="Arquivo CSV de processos"
              onChange={(e) => {
                const arquivo = e.target.files?.[0]
                if (arquivo) void lerArquivo(arquivo)
                e.target.value = ''
              }}
            />
          </div>
          {erro ? (
            <p role="alert" className="text-sm text-destructive">
              {erro}
            </p>
          ) : null}
          <p className="text-sm text-muted-foreground">
            Obrigatórias: <code>numero_cnj</code> e <code>documento_cliente</code> (o cliente
            precisa estar cadastrado). Opcionais: área, tribunal, comarca, vara, assunto,
            parte_contraria, valor_causa e data_distribuicao. O tribunal é deduzido do número quando
            não informado.
          </p>
          <Button type="button" variant="ghost" size="sm" onClick={baixarModelo}>
            <Download aria-hidden /> Baixar modelo
          </Button>
        </CardContent>
      </Card>
    )
  }

  if (passo === 'preview') {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">2. Confira antes de importar</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p role="status" className="text-sm">
            <strong>{importaveis.length}</strong> válidas · <strong>{comErro.length}</strong> com
            erro · <strong>{repetidas.length}</strong> repetidas no arquivo (ignoradas)
          </p>
          <div className="max-h-96 overflow-auto rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Linha</TableHead>
                  <TableHead>CNJ</TableHead>
                  <TableHead>Situação</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {linhas.map((l) => (
                  <TableRow key={l.linha}>
                    <TableCell>{l.linha}</TableCell>
                    <TableCell className="font-mono text-xs">
                      {l.valores.cnjNumber || '—'}
                    </TableCell>
                    <TableCell>
                      {l.erros.length > 0 ? (
                        <StatusBadge tone="danger">{l.erros.join(' ')}</StatusBadge>
                      ) : l.duplicadaNoArquivo ? (
                        <StatusBadge tone="neutral">Repetida no arquivo</StatusBadge>
                      ) : (
                        <StatusBadge tone="success">Pronta</StatusBadge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          <div className="flex flex-wrap gap-3">
            <Button
              type="button"
              disabled={importaveis.length === 0}
              onClick={() => void importar()}
            >
              Importar {importaveis.length} processo(s)
            </Button>
            <Button type="button" variant="ghost" onClick={() => setPasso('upload')}>
              Escolher outro arquivo
            </Button>
          </div>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">3. Resultado</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {!relatorio ? (
          <div className="space-y-2" role="status">
            <p className="text-sm">Importando… {progresso}%</p>
            <Progress value={progresso} aria-label="Progresso da importação" />
          </div>
        ) : (
          <>
            <ul className="space-y-1 text-sm">
              <li>
                <strong>{relatorio.importados}</strong> processo(s) importado(s)
              </li>
              <li>
                <strong>{relatorio.duplicados}</strong> duplicado(s) ignorado(s)
              </li>
              <li>
                <strong>{relatorio.falhas.length}</strong> com erro
                {comErro.length > 0 ? ` (+ ${comErro.length} barrados na conferência)` : ''}
              </li>
            </ul>
            {relatorio.falhas.length > 0 ? (
              <ul className="space-y-1 text-sm text-destructive">
                {relatorio.falhas.map((f) => (
                  <li key={f.linha}>
                    Linha {f.linha} ({f.cnj}): {f.motivo}
                  </li>
                ))}
              </ul>
            ) : null}
            <Button asChild>
              <Link href={routes.processes as never}>Ver processos</Link>
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  )
}

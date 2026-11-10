'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Download, Save, Sparkles } from 'lucide-react'
import Link from 'next/link'
import { useMemo, useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { uploadDocumento } from '@/features/documentos/api'
import { processosQuery } from '@/features/processos/queries'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { gerarMinuta } from '../api'
import { separarCitacoes } from '../citacoes'
import {
  minutaComoTexto,
  nomeDoArquivo,
  tiposPeca,
  type DadosProcesso,
  type Minuta,
  type TipoPeca,
} from '../modelos'

interface OpcaoProcesso extends DadosProcesso {
  id: string
}

/** Processo de exemplo, para a demonstração funcionar mesmo sem a API de processos no ar. */
const PROCESSO_EXEMPLO: OpcaoProcesso = {
  id: 'exemplo',
  cnjNumber: '0000777-07.2018.8.06.0100',
  tribunal: 'TJCE',
  comarca: null,
  vara: null,
  parteContraria: 'Banco Alfa S.A.',
}

function Paragrafo({ texto }: { texto: string }) {
  return (
    <p className="leading-[1.75]">
      {separarCitacoes(texto).map((trecho, indice) =>
        trecho.citacao ? (
          <mark key={indice} className="rounded-xs bg-ia-soft px-0.5 font-medium text-ia-text">
            {trecho.texto}
          </mark>
        ) : (
          trecho.texto
        )
      )}
    </p>
  )
}

/** Sem mocks não há geração de peça: a tela diz isso em vez de entregar um texto de aparência pronta. */
function SemGeracao() {
  return (
    <EmptyState
      icon={Sparkles}
      title="A criação de peças com IA ainda não está disponível"
      description="Não há serviço de geração de minutas ligado a este escritório. Quando houver, a minuta sempre sairá como rascunho, sujeito à sua revisão."
      action={
        <Link href={routes.documents} className="text-sm font-medium underline underline-offset-4">
          Ir para os documentos
        </Link>
      }
    />
  )
}

export function PecasView() {
  const demo = env.useMocks
  const queryClient = useQueryClient()
  const [tipo, setTipo] = useState<TipoPeca>('contestacao')
  const [processoId, setProcessoId] = useState<string>('')
  const [minuta, setMinuta] = useState<Minuta | null>(null)
  const [salva, setSalva] = useState(false)

  const processos = useQuery({
    ...processosQuery({ pageSize: 100, status: 'Ativo' }),
    enabled: demo,
  })
  const opcoes = useMemo<OpcaoProcesso[]>(() => {
    const reais = (processos.data?.items ?? []).map((p) => ({
      id: p.id,
      cnjNumber: p.cnjNumber,
      tribunal: p.tribunal,
      comarca: null,
      vara: null,
      parteContraria: p.parteContraria,
    }))

    return reais.length > 0 ? reais : demo ? [PROCESSO_EXEMPLO] : []
  }, [processos.data, demo])
  const processo = opcoes.find((o) => o.id === processoId) ?? null

  // Trocar o tipo ou o processo invalida a minuta: ela era de OUTRA peça ou de OUTRO processo.
  function escolher(mudanca: () => void) {
    mudanca()
    setMinuta(null)
    setSalva(false)
  }

  const gerar = useMutation({
    mutationFn: () => gerarMinuta(tipo, processo!),
    onSuccess: (resultado) => {
      setMinuta(resultado)
      setSalva(false)
    },
    onError: (error) => handleApiError(error),
  })

  const salvar = useMutation({
    mutationFn: () => {
      const texto = minutaComoTexto(minuta!)
      const arquivo = new File([texto], nomeDoArquivo(minuta!, processo!.cnjNumber), {
        type: 'text/plain',
      })

      return uploadDocumento({
        file: arquivo,
        vinculoTipo: 'Processo',
        vinculoRotulo: processo!.cnjNumber,
      })
    },
    onSuccess: () => {
      setSalva(true)
      void queryClient.invalidateQueries({ queryKey: queryKeys.documentos.all })
      notify.success('Minuta salva em Documentos como rascunho')
    },
    onError: (error) => handleApiError(error),
  })

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-xs font-medium text-muted-foreground">Ferramentas · com IA</p>
        <h1 className="text-[33px] leading-tight font-semibold">Criação de peças</h1>
      </header>

      {!demo ? (
        <SemGeracao />
      ) : (
        <>
          <p
            role="note"
            className="rounded-lg border border-dashed px-4 py-2 text-sm text-muted-foreground"
          >
            <strong className="font-medium text-foreground">Modelos de demonstração.</strong> Não há
            serviço de geração de minutas ligado: o que aparece é um esqueleto com lacunas{' '}
            <span className="font-mono">[...]</span> para você preencher, não uma peça pronta.
          </p>

          <section
            aria-label="Escolha da peça"
            className="grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end"
          >
            <div className="space-y-1.5">
              <label id="tipo-peca-rotulo" className="text-sm font-medium">
                Tipo de peça
              </label>
              <Select
                value={tipo}
                onValueChange={(valor) => escolher(() => setTipo(valor as TipoPeca))}
              >
                <SelectTrigger aria-labelledby="tipo-peca-rotulo" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {tiposPeca.map((opcao) => (
                    <SelectItem key={opcao.value} value={opcao.value}>
                      {opcao.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <label id="processo-peca-rotulo" className="text-sm font-medium">
                Processo
              </label>
              <Select
                value={processoId}
                onValueChange={(valor) => escolher(() => setProcessoId(valor))}
              >
                <SelectTrigger aria-labelledby="processo-peca-rotulo" className="w-full">
                  <SelectValue placeholder="Selecione o processo" />
                </SelectTrigger>
                <SelectContent>
                  {opcoes.map((opcao) => (
                    <SelectItem key={opcao.id} value={opcao.id}>
                      <span className="font-mono">{opcao.cnjNumber}</span>
                      {opcao.parteContraria ? ` — ${opcao.parteContraria}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <Button onClick={() => gerar.mutate()} disabled={!processo || gerar.isPending}>
              {gerar.isPending ? <LoadingSpinner size="sm" decorative /> : <Sparkles aria-hidden />}
              Gerar minuta com IA
            </Button>
          </section>

          {gerar.isPending ? (
            <div
              className="space-y-3 rounded-[10px] border bg-card p-6"
              role="status"
              aria-busy="true"
            >
              <p className="text-sm text-muted-foreground">Gerando a minuta…</p>
              {[100, 92, 96, 70].map((largura, i) => (
                <Skeleton
                  key={i}
                  className="h-4"
                  style={{ width: `${largura}%`, animationDelay: `${i * 0.15}s` }}
                />
              ))}
            </div>
          ) : minuta ? (
            <section
              aria-label="Minuta gerada"
              className="space-y-4 rounded-[10px] border border-t-2 border-t-ia bg-card p-6 shadow-sm"
            >
              <p className="flex items-center gap-2 text-sm font-medium text-ia-text">
                <Sparkles className="size-4" aria-hidden />
                Rascunho gerado por IA — exige revisão do advogado antes de qualquer uso
              </p>
              <h2 className="text-lg font-semibold">{minuta.titulo}</h2>
              <div className="max-w-3xl space-y-3 text-[15px]">
                {minuta.paragrafos.map((texto, indice) => (
                  <Paragrafo key={indice} texto={texto} />
                ))}
              </div>
              <div className="flex flex-wrap items-center gap-3 border-t pt-4">
                <Button onClick={() => salvar.mutate()} disabled={salvar.isPending || salva}>
                  {salvar.isPending ? (
                    <LoadingSpinner size="sm" decorative />
                  ) : (
                    <Save aria-hidden />
                  )}
                  {salva ? 'Salva em Documentos' : 'Salvar como rascunho'}
                </Button>
                <Button variant="outline" disabled title="Exportar para PDF/DOCX ainda não existe">
                  <Download aria-hidden />
                  Exportar PDF/DOCX (em breve)
                </Button>
              </div>
            </section>
          ) : null}
        </>
      )}
    </div>
  )
}

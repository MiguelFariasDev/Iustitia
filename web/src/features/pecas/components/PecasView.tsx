'use client'

import { useMutation, useQuery } from '@tanstack/react-query'
import { AlertTriangle, Copy, Download, Sparkles, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/progress'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { processosQuery } from '@/features/processos/queries'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { cancelarPeca, getPeca, getStatusPeca, submeterPeca } from '../api'
import { separarCitacoes } from '../citacoes'
import {
  areasPeca,
  statusIs,
  statusTerminal,
  tiposPeca,
  type AreaPeca,
  type TipoPeca,
} from '../types'
import { AnexosPeca, useAnexos } from './AnexosPeca'

const POLLING_MS = 2000
const MAX_TEXTO = 8000

function semAcento(valor: string) {
  return valor
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
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

function Campo({
  id,
  rotulo,
  dica,
  children,
}: {
  id: string
  rotulo: string
  dica?: string
  children: React.ReactNode
}) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {rotulo}
      </label>
      {children}
      {dica ? <p className="text-xs text-muted-foreground">{dica}</p> : null}
    </div>
  )
}

/**
 * Criação de peças com a API real (Drafting): os fatos, os pedidos, o COMANDO do advogado
 * (instruções adicionais) e os documentos anexados do processo vão para a geração, que é
 * assíncrona — a tela acompanha o status até a minuta ficar pronta. A minuta é sempre rascunho.
 */
export function PecasView() {
  const [tipo, setTipo] = useState<TipoPeca>('contestacao')
  const [area, setArea] = useState<AreaPeca>('civel')
  const [processoId, setProcessoId] = useState('')
  const [fatos, setFatos] = useState('')
  const [pedidos, setPedidos] = useState('')
  const [comando, setComando] = useState('')
  const [pecaId, setPecaId] = useState<string | null>(null)
  const anexos = useAnexos()

  const processos = useQuery(processosQuery({ pageSize: 100, status: 'Ativo' }))
  const opcoesProcesso = useMemo(() => processos.data?.items ?? [], [processos.data])

  function escolherProcesso(id: string) {
    setProcessoId(id)
    const processo = opcoesProcesso.find((p) => p.id === id)
    if (!processo) return
    // Só preenche o que está vazio: nunca sobrescreve o que o advogado já escreveu.
    const areaDoProcesso = areasPeca.find((a) => a.value === semAcento(processo.area ?? ''))
    if (areaDoProcesso) setArea(areaDoProcesso.value)
    if (fatos.trim() === '') {
      const contraria = processo.parteContraria
        ? ` Parte contrária: ${processo.parteContraria}.`
        : ''
      const tribunal = processo.tribunal ? ` Tribunal: ${processo.tribunal}.` : ''
      setFatos(`Processo nº ${processo.cnjNumber}.${tribunal}${contraria}\n`)
    }
  }

  const status = useQuery({
    queryKey: ['pecas', 'status', pecaId],
    queryFn: () => getStatusPeca(pecaId!),
    enabled: pecaId !== null,
    refetchInterval: (consulta) =>
      statusTerminal(consulta.state.data?.status) ? false : POLLING_MS,
  })
  const concluida = statusIs(status.data?.status, 'concluido')
  const emAndamento = pecaId !== null && !statusTerminal(status.data?.status)

  const peca = useQuery({
    queryKey: ['pecas', 'detalhe', pecaId],
    queryFn: () => getPeca(pecaId!),
    enabled: pecaId !== null && concluida,
  })

  const gerar = useMutation({
    mutationFn: () =>
      submeterPeca({
        tipoPeca: tipo,
        area,
        fatos: fatos.trim(),
        pedidos: pedidos.trim() || undefined,
        instrucoesAdicionais: comando.trim() || undefined,
        documentosIds: anexos.ids.length > 0 ? anexos.ids : undefined,
      }),
    onSuccess: (resposta) => setPecaId(resposta.id),
    onError: (error) => handleApiError(error),
  })

  const cancelar = useMutation({
    mutationFn: () => cancelarPeca(pecaId!),
    onSuccess: () => {
      void status.refetch()
      notify.info('Geração cancelada')
    },
    onError: (error) => handleApiError(error),
  })

  const podeGerar = fatos.trim().length > 0 && !anexos.ocupado && !gerar.isPending && !emAndamento

  function nova() {
    setPecaId(null)
    gerar.reset()
  }

  const texto = peca.data?.textoGerado ?? ''
  const bloqueada = peca.data?.exportacaoBloqueada ?? false

  async function copiar() {
    try {
      await navigator.clipboard.writeText(texto)
      notify.success('Minuta copiada')
    } catch {
      notify.error('Não foi possível copiar')
    }
  }

  function baixar() {
    const arquivo = new Blob([texto], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(arquivo)
    const ancora = document.createElement('a')
    ancora.href = url
    ancora.download = `minuta-${tipo}-${pecaId?.slice(0, 8) ?? 'rascunho'}.txt`
    ancora.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-xs font-medium text-muted-foreground">Ferramentas · com IA</p>
        <h1 className="text-[33px] leading-tight font-semibold">Criação de peças</h1>
      </header>

      <form
        className="space-y-5"
        aria-label="Dados da peça"
        onSubmit={(evento) => {
          evento.preventDefault()
          if (podeGerar) gerar.mutate()
        }}
      >
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-1.5">
            <label id="tipo-peca-rotulo" className="text-sm font-medium">
              Tipo de peça
            </label>
            <Select value={tipo} onValueChange={(valor) => setTipo(valor as TipoPeca)}>
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
            <label id="area-peca-rotulo" className="text-sm font-medium">
              Área
            </label>
            <Select value={area} onValueChange={(valor) => setArea(valor as AreaPeca)}>
              <SelectTrigger aria-labelledby="area-peca-rotulo" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {areasPeca.map((opcao) => (
                  <SelectItem key={opcao.value} value={opcao.value}>
                    {opcao.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <label id="processo-peca-rotulo" className="text-sm font-medium">
              Processo (opcional)
            </label>
            <Select value={processoId} onValueChange={escolherProcesso}>
              <SelectTrigger aria-labelledby="processo-peca-rotulo" className="w-full">
                <SelectValue
                  placeholder={
                    opcoesProcesso.length === 0 ? 'Nenhum processo ativo' : 'Selecione o processo'
                  }
                />
              </SelectTrigger>
              <SelectContent>
                {opcoesProcesso.map((opcao) => (
                  <SelectItem key={opcao.id} value={opcao.id}>
                    <span className="font-mono">{opcao.cnjNumber}</span>
                    {opcao.parteContraria ? ` — ${opcao.parteContraria}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <Campo
          id="fatos-peca"
          rotulo="Fatos do caso"
          dica="Obrigatório. Descreva o que aconteceu; a IA não inventa fatos que você não informar."
        >
          <Textarea
            id="fatos-peca"
            value={fatos}
            maxLength={MAX_TEXTO}
            rows={6}
            onChange={(evento) => setFatos(evento.target.value)}
          />
        </Campo>

        <Campo
          id="pedidos-peca"
          rotulo="Pedidos"
          dica="Recomendado. Sem pedidos, a IA é instruída a não criá-los."
        >
          <Textarea
            id="pedidos-peca"
            value={pedidos}
            maxLength={MAX_TEXTO}
            rows={3}
            onChange={(evento) => setPedidos(evento.target.value)}
          />
        </Campo>

        <Campo
          id="comando-peca"
          rotulo="Comando do advogado"
          dica="Instruções livres para a IA: tese a defender, tom, estrutura, o que enfatizar ou evitar."
        >
          <Textarea
            id="comando-peca"
            value={comando}
            maxLength={MAX_TEXTO}
            rows={4}
            placeholder="Ex.: Priorize a preliminar de ilegitimidade passiva e use tom firme, porém cordial."
            onChange={(evento) => setComando(evento.target.value)}
          />
        </Campo>

        <AnexosPeca
          anexos={anexos.anexos}
          onAdicionar={anexos.adicionar}
          onRemover={(anexo) => void anexos.remover(anexo)}
          disabled={emAndamento}
        />

        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit" disabled={!podeGerar}>
            {gerar.isPending ? <LoadingSpinner size="sm" decorative /> : <Sparkles aria-hidden />}
            Gerar minuta com IA
          </Button>
          {anexos.ocupado ? (
            <p role="status" className="text-xs text-muted-foreground">
              Aguardando os documentos terminarem de ser processados…
            </p>
          ) : null}
        </div>
      </form>

      {pecaId !== null ? (
        <section aria-label="Resultado da geração" className="space-y-4">
          {emAndamento ? (
            <div
              className="space-y-3 rounded-[10px] border bg-card p-6"
              role="status"
              aria-busy="true"
            >
              <p className="text-sm font-medium">{status.data?.mensagem ?? 'Enviando o pedido…'}</p>
              <Progress value={status.data?.progresso ?? 0} aria-label="Progresso da geração" />
              <p className="text-xs text-muted-foreground">
                A geração roda em segundo plano e pode levar alguns minutos.
              </p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => cancelar.mutate()}
                disabled={cancelar.isPending}
              >
                <X aria-hidden />
                Cancelar
              </Button>
            </div>
          ) : statusIs(status.data?.status, 'falhou') ? (
            <div role="alert" className="space-y-3 rounded-[10px] border border-destructive/40 p-6">
              <p className="flex items-center gap-2 text-sm font-medium text-destructive">
                <AlertTriangle className="size-4" aria-hidden />A geração falhou
              </p>
              <p className="text-sm text-muted-foreground">
                {status.data?.erro ?? status.data?.mensagem ?? 'Tente novamente em instantes.'}
              </p>
              <Button type="button" variant="outline" size="sm" onClick={nova}>
                Tentar de novo
              </Button>
            </div>
          ) : statusIs(status.data?.status, 'cancelado') ? (
            <div className="space-y-3 rounded-[10px] border p-6">
              <p className="text-sm">Geração cancelada.</p>
              <Button type="button" variant="outline" size="sm" onClick={nova}>
                Voltar
              </Button>
            </div>
          ) : concluida && peca.data ? (
            <article
              aria-label="Minuta gerada"
              className="space-y-4 rounded-[10px] border border-t-2 border-t-ia bg-card p-6 shadow-sm"
            >
              <p className="flex items-center gap-2 text-sm font-medium text-ia-text">
                <Sparkles className="size-4" aria-hidden />
                Rascunho gerado por IA — exige revisão do advogado antes de qualquer uso
              </p>
              <div className="max-w-3xl space-y-3 text-[15px]">
                {texto
                  .split(/\n{2,}/)
                  .filter((trecho) => trecho.trim() !== '')
                  .map((trecho, indice) => (
                    <Paragrafo key={indice} texto={trecho} />
                  ))}
              </div>

              {peca.data.avisos.length > 0 ? (
                <ul className="space-y-1 rounded-lg border border-warning/40 bg-warning/5 p-3 text-sm">
                  {peca.data.avisos.map((aviso) => (
                    <li key={aviso}>{aviso}</li>
                  ))}
                </ul>
              ) : null}
              <p className="text-xs text-muted-foreground">{peca.data.aviso}</p>

              {peca.data.fontes.length > 0 ? (
                <details className="rounded-lg border p-3 text-sm">
                  <summary className="cursor-pointer font-medium">
                    Fontes usadas ({peca.data.fontes.length})
                  </summary>
                  <ul className="mt-2 space-y-2">
                    {peca.data.fontes.map((fonte) => (
                      <li key={fonte.id}>
                        <span className="font-medium">
                          {fonte.nomeArquivo}
                          {fonte.pagina ? ` · p. ${fonte.pagina}` : ''}
                          {fonte.citada ? ' · citada' : ''}
                        </span>
                        <span className="block text-xs text-muted-foreground">{fonte.trecho}</span>
                      </li>
                    ))}
                  </ul>
                </details>
              ) : null}

              <div className="flex flex-wrap items-center gap-3 border-t pt-4">
                <Button type="button" onClick={() => void copiar()} disabled={bloqueada}>
                  <Copy aria-hidden />
                  Copiar texto
                </Button>
                <Button type="button" variant="outline" onClick={baixar} disabled={bloqueada}>
                  <Download aria-hidden />
                  Baixar (.txt)
                </Button>
                <Button type="button" variant="ghost" onClick={nova}>
                  Nova peça
                </Button>
                {bloqueada ? (
                  <p className="text-xs text-muted-foreground">
                    Exportação bloqueada: revise os avisos acima.
                  </p>
                ) : null}
              </div>
            </article>
          ) : (
            <div role="status" aria-busy="true" className="text-sm text-muted-foreground">
              Carregando a minuta…
            </div>
          )}
        </section>
      ) : null}
    </div>
  )
}

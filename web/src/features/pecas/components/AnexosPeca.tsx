'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, FileText, Paperclip, X } from 'lucide-react'
import { useRef, useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { getErrorMessage } from '@/lib/api/errors'
import { handleApiError } from '@/lib/api/toastHandler'
import { confirmarRevisaoAnexo, enviarAnexo, getTextoAnexo, removerAnexo } from '../api'
import { anexosQuery } from '../queries'
import { anexoTerminado, EXTENSOES_ANEXO, type AnexoCaso } from '../types'

const rotuloStatus: Record<string, string> = {
  Recebido: 'Na fila de leitura…',
  Convertendo: 'Lendo o documento…',
  Convertido: 'Na fila de indexação…',
  Indexando: 'Indexando…',
  Indexado: 'Pronto para uso',
  Falhou: 'Não foi possível ler',
}

function precisaConferir(anexo: AnexoCaso) {
  return anexo.status === 'Indexado' && anexo.requerRevisaoHumana && !anexo.revisado
}

function descricao(anexo: AnexoCaso) {
  if (anexo.status !== 'Indexado')
    return anexo.mensagem ?? rotuloStatus[anexo.status] ?? anexo.status

  const partes = [rotuloStatus.Indexado]
  if (anexo.tipoDocumento) partes.push(anexo.tipoDocumento.replaceAll('_', ' '))
  if (anexo.origem === 'ocr') partes.push(`lido por OCR (confiança ${anexo.confiancaOcr})`)
  if (anexo.origem === 'transcricao') partes.push('transcrição — não é a prova original')

  return partes.join(' · ')
}

/**
 * Estado dos anexos do caso: o que já existe no servidor, os envios em andamento e a seleção que
 * restringe a busca. Sem seleção, a IA considera todos os anexos do caso.
 */
export function useAnexos(casoId: string) {
  const queryClient = useQueryClient()
  const lista = useQuery(anexosQuery(casoId))
  const [enviando, setEnviando] = useState<{ chave: string; nome: string; erro?: string }[]>([])
  const [ignorados, setIgnorados] = useState<ReadonlySet<string>>(new Set())

  const recarregar = () =>
    queryClient.invalidateQueries({ queryKey: ['casos', casoId, 'documentos'] })

  function adicionar(arquivos: File[]) {
    for (const arquivo of arquivos) {
      const chave = crypto.randomUUID()
      setEnviando((atual) => [...atual, { chave, nome: arquivo.name }])
      enviarAnexo(casoId, arquivo)
        .then(async () => {
          await recarregar()
          setEnviando((atual) => atual.filter((e) => e.chave !== chave))
        })
        .catch((error: unknown) =>
          setEnviando((atual) =>
            atual.map((e) => (e.chave === chave ? { ...e, erro: getErrorMessage(error) } : e))
          )
        )
    }
  }

  const anexos = lista.data ?? []
  const indexados = anexos.filter((anexo) => anexo.status === 'Indexado')
  const selecionados = indexados.filter((anexo) => !ignorados.has(anexo.id))

  return {
    anexos,
    enviando,
    ignorados,
    recarregar,
    adicionar,
    descartarEnvio: (chave: string) =>
      setEnviando((atual) => atual.filter((e) => e.chave !== chave)),
    alternar: (id: string) =>
      setIgnorados((atual) => {
        const novo = new Set(atual)
        if (!novo.delete(id)) novo.add(id)

        return novo
      }),
    ocupado: enviando.some((e) => !e.erro) || anexos.some((anexo) => !anexoTerminado(anexo.status)),
    /** Só manda a lista quando a pessoa desmarcou algum: todos marcados = sem restrição. */
    ids:
      selecionados.length > 0 && selecionados.length < indexados.length
        ? selecionados.map((anexo) => anexo.id)
        : undefined,
  }
}

export type EstadoAnexos = ReturnType<typeof useAnexos>

export function AnexosPeca({
  casoId,
  estado,
  disabled,
}: {
  casoId: string
  estado: EstadoAnexos
  disabled?: boolean
}) {
  const entrada = useRef<HTMLInputElement>(null)
  const [conferindo, setConferindo] = useState<{ id: string; texto: string } | null>(null)

  const remover = useMutation({
    mutationFn: (id: string) => removerAnexo(casoId, id),
    onSuccess: () => estado.recarregar(),
    onError: (error) => handleApiError(error),
  })
  const abrir = useMutation({
    mutationFn: async (id: string) => ({ id, texto: await getTextoAnexo(casoId, id) }),
    onSuccess: setConferindo,
    onError: (error) => handleApiError(error),
  })
  const confirmar = useMutation({
    mutationFn: (id: string) => confirmarRevisaoAnexo(casoId, id),
    onSuccess: async () => {
      setConferindo(null)
      await estado.recarregar()
    },
    onError: (error) => handleApiError(error),
  })

  return (
    <section aria-labelledby="anexos-titulo" className="space-y-3">
      <div className="space-y-1">
        <h2 id="anexos-titulo" className="text-sm font-medium">
          Documentos do caso
        </h2>
        <p className="text-xs text-muted-foreground">
          PDF (inclusive escaneado), Word, texto, imagem, planilha, e-mail ou áudio. Desmarque o que
          a IA não deve usar nesta peça.
        </p>
      </div>

      <input
        ref={entrada}
        type="file"
        multiple
        accept={EXTENSOES_ANEXO}
        className="sr-only"
        aria-label="Selecionar documentos do caso"
        tabIndex={-1}
        disabled={casoId === ''}
        onChange={(evento) => {
          const arquivos = Array.from(evento.target.files ?? [])
          if (arquivos.length > 0) estado.adicionar(arquivos)
          evento.target.value = ''
        }}
      />
      <Button
        type="button"
        variant="outline"
        disabled={disabled || casoId === ''}
        onClick={() => entrada.current?.click()}
      >
        <Paperclip aria-hidden />
        Anexar documentos
      </Button>

      {estado.anexos.length > 0 || estado.enviando.length > 0 ? (
        <ul className="divide-y rounded-lg border bg-card">
          {estado.enviando.map((envio) => (
            <li key={envio.chave} className="flex items-center gap-3 px-3 py-2">
              <FileText className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium">{envio.nome}</span>
                <span
                  className={
                    envio.erro
                      ? 'block text-xs text-destructive'
                      : 'block text-xs text-muted-foreground'
                  }
                >
                  {envio.erro ?? 'Enviando…'}
                </span>
              </span>
              {envio.erro ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Descartar ${envio.nome}`}
                  onClick={() => estado.descartarEnvio(envio.chave)}
                >
                  <X aria-hidden />
                </Button>
              ) : null}
            </li>
          ))}
          {estado.anexos.map((anexo) => (
            <li key={anexo.id} className="space-y-2 px-3 py-2">
              <div className="flex items-center gap-3">
                <Checkbox
                  aria-label={`Usar ${anexo.nomeArquivo} nesta peça`}
                  checked={anexo.status === 'Indexado' && !estado.ignorados.has(anexo.id)}
                  disabled={disabled || anexo.status !== 'Indexado'}
                  onCheckedChange={() => estado.alternar(anexo.id)}
                />
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium">{anexo.nomeArquivo}</span>
                  <span
                    className={
                      anexo.status === 'Falhou'
                        ? 'block text-xs text-destructive'
                        : 'block text-xs text-muted-foreground'
                    }
                  >
                    {anexoTerminado(anexo.status) ? null : (
                      <LoadingSpinner size="sm" decorative className="mr-1.5 inline-block" />
                    )}
                    {descricao(anexo)}
                  </span>
                </span>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remover ${anexo.nomeArquivo}`}
                  disabled={disabled || remover.isPending}
                  onClick={() => remover.mutate(anexo.id)}
                >
                  <X aria-hidden />
                </Button>
              </div>

              {precisaConferir(anexo) ? (
                <div className="space-y-2 rounded-md border border-warning/40 bg-warning/5 p-2 text-xs">
                  <p className="flex items-center gap-2">
                    <AlertTriangle className="size-4 shrink-0" aria-hidden />
                    {anexo.conteudoSuspeito
                      ? 'O documento contém texto que tenta dar instruções à IA. Confira antes de usar.'
                      : 'A leitura automática pode ter erros. Confira o texto com o original.'}
                  </p>
                  {conferindo?.id === anexo.id ? (
                    <>
                      <pre className="max-h-64 overflow-auto rounded border bg-card p-2 whitespace-pre-wrap">
                        {conferindo.texto}
                      </pre>
                      <Button
                        type="button"
                        size="sm"
                        disabled={confirmar.isPending}
                        onClick={() => confirmar.mutate(anexo.id)}
                      >
                        Conferi com o original
                      </Button>
                    </>
                  ) : (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={abrir.isPending}
                      onClick={() => abrir.mutate(anexo.id)}
                    >
                      Ver texto extraído
                    </Button>
                  )}
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  )
}

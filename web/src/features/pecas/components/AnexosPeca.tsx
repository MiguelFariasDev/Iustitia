'use client'

import { FileText, Paperclip, X } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { getErrorMessage } from '@/lib/api/errors'
import { enviarAnexo, getAnexo, removerAnexo } from '../api'
import { EXTENSOES_ANEXO, statusIs, tiposAnexo, type TipoAnexo } from '../types'

export type SituacaoAnexo = 'Enviando' | 'Pendente' | 'Indexando' | 'Indexado' | 'Falhou' | 'Erro'

export interface Anexo {
  chave: string
  nome: string
  tipo: TipoAnexo
  situacao: SituacaoAnexo
  /** Id no acervo; só existe depois que o envio terminou. */
  id?: string
  erro?: string
}

const POLLING_MS = 3000

function situacaoDe(status: string): SituacaoAnexo {
  if (statusIs(status, 'indexado')) return 'Indexado'
  if (statusIs(status, 'indexando')) return 'Indexando'
  if (statusIs(status, 'falhou')) return 'Falhou'

  return 'Pendente'
}

const rotuloSituacao: Record<SituacaoAnexo, string> = {
  Enviando: 'Enviando…',
  Pendente: 'Na fila de indexação…',
  Indexando: 'Indexando…',
  Indexado: 'Pronto para uso',
  Falhou: 'Falhou ao indexar',
  Erro: 'Não foi enviado',
}

/**
 * Anexos de uma peça: sobem para o acervo na hora, e só os INDEXADOS entram na geração (o backend
 * só enxerga o que já foi indexado). Enquanto algum estiver em andamento, `prontos` é falso e a
 * tela segura o botão de gerar em vez de gerar sem o documento e não avisar.
 */
export function useAnexos() {
  const [anexos, setAnexos] = useState<Anexo[]>([])
  const anexosRef = useRef(anexos)
  useEffect(() => {
    anexosRef.current = anexos
  }, [anexos])

  const atualizar = useCallback((chave: string, mudanca: Partial<Anexo>) => {
    setAnexos((lista) => lista.map((a) => (a.chave === chave ? { ...a, ...mudanca } : a)))
  }, [])

  const adicionar = useCallback(
    (arquivos: File[], tipo: TipoAnexo) => {
      const novos = arquivos.map<Anexo>((arquivo) => ({
        chave: `${arquivo.name}-${arquivo.size}-${crypto.randomUUID()}`,
        nome: arquivo.name,
        tipo,
        situacao: 'Enviando',
      }))
      setAnexos((lista) => [...lista, ...novos])

      novos.forEach((anexo, indice) => {
        enviarAnexo(arquivos[indice]!, tipo)
          .then(({ id, status }) => atualizar(anexo.chave, { id, situacao: situacaoDe(status) }))
          .catch((error: unknown) =>
            atualizar(anexo.chave, { situacao: 'Erro', erro: getErrorMessage(error) })
          )
      })
    },
    [atualizar]
  )

  const remover = useCallback(async (anexo: Anexo) => {
    setAnexos((lista) => lista.filter((a) => a.chave !== anexo.chave))
    if (anexo.id) {
      try {
        await removerAnexo(anexo.id)
      } catch {
        // Já saiu da lista; se a remoção no acervo falhar, o documento só fica sem uso.
      }
    }
  }, [])

  const limpar = useCallback(() => setAnexos([]), [])

  // Acompanha a indexação dos que ainda não terminaram.
  const emAndamento = anexos.some(
    (a) => a.id && (a.situacao === 'Pendente' || a.situacao === 'Indexando')
  )
  useEffect(() => {
    if (!emAndamento) return
    const timer = setInterval(() => {
      for (const anexo of anexosRef.current) {
        if (!anexo.id || (anexo.situacao !== 'Pendente' && anexo.situacao !== 'Indexando')) continue
        getAnexo(anexo.id)
          .then((doc) => atualizar(anexo.chave, { situacao: situacaoDe(doc.status) }))
          .catch(() => undefined)
      }
    }, POLLING_MS)

    return () => clearInterval(timer)
  }, [emAndamento, atualizar])

  const ocupado = anexos.some(
    (a) => a.situacao === 'Enviando' || a.situacao === 'Pendente' || a.situacao === 'Indexando'
  )
  const ids = anexos.filter((a) => a.situacao === 'Indexado' && a.id).map((a) => a.id!)

  return { anexos, adicionar, remover, limpar, ocupado, ids }
}

export function AnexosPeca({
  anexos,
  onAdicionar,
  onRemover,
  disabled,
}: {
  anexos: Anexo[]
  onAdicionar: (arquivos: File[], tipo: TipoAnexo) => void
  onRemover: (anexo: Anexo) => void
  disabled?: boolean
}) {
  const [tipo, setTipo] = useState<TipoAnexo>('peticao')
  const entrada = useRef<HTMLInputElement>(null)

  return (
    <section aria-labelledby="anexos-titulo" className="space-y-3">
      <div className="space-y-1">
        <h2 id="anexos-titulo" className="text-sm font-medium">
          Documentos do processo
        </h2>
        <p className="text-xs text-muted-foreground">
          Anexe petições, sentenças, contratos e outros documentos do caso (PDF com texto, DOCX ou
          TXT). A IA usa o conteúdo deles como base da peça.
        </p>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1.5">
          <label id="tipo-anexo-rotulo" className="text-xs font-medium text-muted-foreground">
            Tipo dos documentos
          </label>
          <Select value={tipo} onValueChange={(valor) => setTipo(valor as TipoAnexo)}>
            <SelectTrigger aria-labelledby="tipo-anexo-rotulo" className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {tiposAnexo.map((opcao) => (
                <SelectItem key={opcao.value} value={opcao.value}>
                  {opcao.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <input
          ref={entrada}
          type="file"
          multiple
          accept={EXTENSOES_ANEXO}
          className="sr-only"
          aria-label="Selecionar documentos do processo"
          tabIndex={-1}
          onChange={(evento) => {
            const arquivos = Array.from(evento.target.files ?? [])
            if (arquivos.length > 0) onAdicionar(arquivos, tipo)
            evento.target.value = ''
          }}
        />
        <Button
          type="button"
          variant="outline"
          disabled={disabled}
          onClick={() => entrada.current?.click()}
        >
          <Paperclip aria-hidden />
          Anexar documentos
        </Button>
      </div>

      {anexos.length > 0 ? (
        <ul className="divide-y rounded-lg border bg-card">
          {anexos.map((anexo) => (
            <li key={anexo.chave} className="flex items-center gap-3 px-3 py-2">
              <FileText className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium">{anexo.nome}</span>
                <span
                  className={
                    anexo.situacao === 'Falhou' || anexo.situacao === 'Erro'
                      ? 'block text-xs text-destructive'
                      : 'block text-xs text-muted-foreground'
                  }
                >
                  {anexo.situacao === 'Enviando' ||
                  anexo.situacao === 'Pendente' ||
                  anexo.situacao === 'Indexando' ? (
                    <LoadingSpinner size="sm" decorative className="mr-1.5 inline-block" />
                  ) : null}
                  {anexo.erro ?? rotuloSituacao[anexo.situacao]}
                </span>
              </span>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Remover ${anexo.nome}`}
                disabled={disabled}
                onClick={() => onRemover(anexo)}
              >
                <X aria-hidden />
              </Button>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  )
}

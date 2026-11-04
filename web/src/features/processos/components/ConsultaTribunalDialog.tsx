'use client'

import { AlertTriangle, FileSearch, Landmark } from 'lucide-react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { ApiError } from '@/lib/api/errors'
import { formatDate } from '@/lib/format'
import type { ConsultaTribunalResult, ProcessoTribunal } from '../datajud'

interface ConsultaTribunalDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  isPending: boolean
  result: ConsultaTribunalResult | undefined
  error: unknown
  /** Tribunal derivado do número, disponível mesmo antes da resposta. */
  tribunal: string | null
  onUsarDados: (processo: ProcessoTribunal) => void
}

/**
 * Mostra o que o tribunal devolveu ANTES de tocar no formulário. O preenchimento automático
 * só acontece com confirmação: os dados vêm de uma base pública que pode estar
 * desatualizada, e sobrescrever silenciosamente o que a advogada digitou seria pior que não
 * consultar.
 */
export function ConsultaTribunalDialog({
  open,
  onOpenChange,
  isPending,
  result,
  error,
  tribunal,
  onUsarDados,
}: ConsultaTribunalDialogProps) {
  const processo = result?.processo ?? null

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Consulta ao tribunal</DialogTitle>
          <DialogDescription>
            {tribunal
              ? `Metadados públicos do processo no ${tribunal}, via DataJud (CNJ).`
              : 'Metadados públicos do processo, via DataJud (CNJ).'}
          </DialogDescription>
        </DialogHeader>

        {isPending ? (
          <div className="flex flex-col items-center gap-3 py-10">
            <LoadingSpinner label="Consultando o tribunal…" />
            <p className="text-sm text-muted-foreground">
              A consulta pública pode levar alguns segundos.
            </p>
          </div>
        ) : error ? (
          <ConsultaErro error={error} />
        ) : processo ? (
          <ProcessoEncontrado processo={processo} />
        ) : (
          <NaoEncontrado tribunal={result?.tribunal ?? tribunal} />
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {processo ? 'Preencher manualmente' : 'Fechar'}
          </Button>
          {processo ? (
            <Button
              onClick={() => {
                onUsarDados(processo)
                onOpenChange(false)
              }}
            >
              Usar estes dados
            </Button>
          ) : null}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function ProcessoEncontrado({ processo }: { processo: ProcessoTribunal }) {
  return (
    <div className="space-y-4">
      {processo.emSegredoDeJustica ? (
        <p className="flex items-start gap-2 rounded-lg border border-dashed px-3 py-2 text-sm text-muted-foreground">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          Processo em segredo de justiça — o tribunal pode ter omitido parte dos dados.
        </p>
      ) : null}

      <dl className="grid gap-3 sm:grid-cols-2">
        <Campo rotulo="Tribunal" valor={processo.tribunal} />
        <Campo rotulo="Grau" valor={processo.grau} />
        <Campo rotulo="Classe" valor={processo.classe} className="sm:col-span-2" />
        <Campo rotulo="Órgão julgador" valor={processo.orgaoJulgador} className="sm:col-span-2" />
        <Campo
          rotulo="Distribuição"
          valor={processo.dataAjuizamento ? formatDate(processo.dataAjuizamento) : null}
        />
        <Campo rotulo="Sistema" valor={processo.sistema} />
      </dl>

      {processo.assuntos.length > 0 ? (
        <div className="space-y-1.5">
          <p className="text-xs font-medium text-muted-foreground">Assuntos</p>
          <div className="flex flex-wrap gap-1.5">
            {processo.assuntos.map((assunto) => (
              <StatusBadge key={assunto} tone="info" hideDot>
                {assunto}
              </StatusBadge>
            ))}
          </div>
        </div>
      ) : null}

      {processo.movimentos.length > 0 ? (
        <div className="space-y-2">
          <p className="text-xs font-medium text-muted-foreground">
            Últimos movimentos ({processo.movimentos.length})
          </p>
          <ol className="space-y-2 border-l pl-3">
            {processo.movimentos.map((movimento, indice) => (
              <li key={`${movimento.nome}-${indice}`} className="text-sm">
                <span className="font-medium">{movimento.nome}</span>
                {movimento.dataHora ? (
                  <time
                    dateTime={movimento.dataHora}
                    className="ml-2 text-xs text-muted-foreground"
                  >
                    {formatDate(movimento.dataHora)}
                  </time>
                ) : null}
              </li>
            ))}
          </ol>
        </div>
      ) : null}

      <p className="text-xs text-muted-foreground">
        Cliente e responsável não vêm do tribunal — continuam sendo preenchidos por você.
      </p>
    </div>
  )
}

function NaoEncontrado({ tribunal }: { tribunal: string | null }) {
  return (
    <div className="flex flex-col items-center gap-3 py-8 text-center">
      <span className="flex size-12 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
        <FileSearch className="size-6" aria-hidden />
      </span>
      <div className="max-w-sm space-y-1">
        <p className="font-medium">
          {tribunal ? `Não encontrado no ${tribunal}` : 'Processo não encontrado'}
        </p>
        <p className="text-sm text-muted-foreground">
          Nem todo processo está na base pública do CNJ — depende do que o tribunal enviou. Confira
          o número ou preencha os dados manualmente.
        </p>
      </div>
    </div>
  )
}

function ConsultaErro({ error }: { error: unknown }) {
  const mensagem =
    error instanceof ApiError
      ? error.message
      : 'Não foi possível consultar o tribunal agora. Tente de novo ou preencha manualmente.'

  return (
    <div className="flex flex-col items-center gap-3 py-8 text-center">
      <span className="flex size-12 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
        <Landmark className="size-6" aria-hidden />
      </span>
      <div className="max-w-sm space-y-1">
        <p className="font-medium">Consulta indisponível</p>
        <p className="text-sm text-muted-foreground">{mensagem}</p>
      </div>
    </div>
  )
}

function Campo({
  rotulo,
  valor,
  className,
}: {
  rotulo: string
  valor: string | null
  className?: string
}) {
  return (
    <div className={className}>
      <dt className="text-xs font-medium text-muted-foreground">{rotulo}</dt>
      <dd className="text-sm">{valor ?? '—'}</dd>
    </div>
  )
}

import { Download, File, FileImage, FileText, Trash2, type LucideIcon } from 'lucide-react'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatDate } from '@/lib/format'
import { formatTamanho } from '../arquivo'
import { tipoVinculoLabels, type Documento, type TipoArquivo } from '../types'

const icones: Record<TipoArquivo, LucideIcon> = {
  PDF: FileText,
  DOCX: FileText,
  Imagem: FileImage,
  Outro: File,
}

interface DocumentoCardProps {
  documento: Documento
  onDownload: (documento: Documento) => void
  onDelete: (documento: Documento) => void
}

export function DocumentoCard({ documento, onDownload, onDelete }: DocumentoCardProps) {
  const Icone = icones[documento.tipoArquivo]
  const { vinculo } = documento

  return (
    <Card className="h-full iu-lift">
      <CardContent className="flex h-full flex-col gap-3 px-4">
        <div className="flex items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-secondary/10 text-secondary">
            <Icone className="size-5" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            <h3 className="text-sm font-medium break-words">{documento.nome}</h3>
            <p className="text-xs text-muted-foreground">
              {documento.tipoArquivo} · {formatTamanho(documento.tamanhoBytes)}
            </p>
          </div>
        </div>

        <dl className="space-y-1 text-xs text-muted-foreground">
          <div className="flex gap-1">
            <dt>Enviado em</dt>
            <dd>
              <time dateTime={documento.criadoEm}>{formatDate(documento.criadoEm)}</time>
            </dd>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <dt>Vínculo</dt>
            <dd className="flex flex-wrap items-center gap-1.5">
              <StatusBadge tone={vinculo.tipo === 'Avulso' ? 'neutral' : 'info'} hideDot>
                {tipoVinculoLabels[vinculo.tipo]}
              </StatusBadge>
              {vinculo.rotulo ? (
                <span className="font-mono break-all text-foreground">{vinculo.rotulo}</span>
              ) : null}
            </dd>
          </div>
        </dl>

        <div className="mt-auto flex justify-end gap-1 pt-1">
          <Button
            variant="ghost"
            size="icon"
            aria-label={`Baixar ${documento.nome}`}
            onClick={() => onDownload(documento)}
          >
            <Download aria-hidden />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label={`Excluir ${documento.nome}`}
            onClick={() => onDelete(documento)}
          >
            <Trash2 aria-hidden />
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

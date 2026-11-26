'use client'

import { useMutation } from '@tanstack/react-query'
import { CheckCircle2, Download } from 'lucide-react'
import { useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { handleApiError } from '@/lib/api/toastHandler'
import { concluirRevisao, decidirCitacao, exportarPeca } from '../api'
import {
  rotuloStatusCitacao,
  type CitacaoPeca,
  type DecisaoCitacao,
  type PecaDetalhe,
} from '../types'

const decisoes: { value: Exclude<DecisaoCitacao, 'pendente'>; label: string }[] = [
  { value: 'confirmada', label: 'Confirmar' },
  { value: 'corrigida', label: 'Corrigi no texto' },
  { value: 'rejeitada', label: 'Rejeitar' },
]

const JUSTIFICATIVA_MIN = 5

function ItemCitacao({
  pecaId,
  citacao,
  onDecidida,
}: {
  pecaId: string
  citacao: CitacaoPeca
  onDecidida: () => void
}) {
  const [justificativa, setJustificativa] = useState(citacao.justificativa ?? '')
  const decidir = useMutation({
    mutationFn: (decisao: Exclude<DecisaoCitacao, 'pendente'>) =>
      decidirCitacao(pecaId, citacao.id, decisao, justificativa.trim()),
    onSuccess: onDecidida,
    onError: (error) => handleApiError(error),
  })
  const campo = `justificativa-${citacao.id}`

  return (
    <li className="space-y-2 py-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={citacao.bloqueante ? 'destructive' : 'secondary'}>
          {rotuloStatusCitacao[citacao.status]}
        </Badge>
        <span className="text-sm font-medium">{citacao.texto}</span>
        {citacao.decisao !== 'pendente' ? (
          <span className="flex items-center gap-1 text-xs text-muted-foreground">
            <CheckCircle2 className="size-3.5" aria-hidden />
            {citacao.decisao}
          </span>
        ) : null}
      </div>
      <p className="text-xs text-muted-foreground">
        {citacao.detalhe}{' '}
        {citacao.fonteUrl ? (
          <a
            href={citacao.fonteUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="underline"
          >
            Abrir fonte oficial
          </a>
        ) : null}
      </p>
      {citacao.bloqueante ? (
        <div className="flex flex-wrap items-end gap-2">
          <div className="min-w-56 flex-1 space-y-1">
            <label htmlFor={campo} className="text-xs font-medium">
              Como você conferiu
            </label>
            <Input
              id={campo}
              value={justificativa}
              maxLength={1000}
              onChange={(evento) => setJustificativa(evento.target.value)}
            />
          </div>
          {decisoes.map((opcao) => (
            <Button
              key={opcao.value}
              type="button"
              size="sm"
              variant={citacao.decisao === opcao.value ? 'default' : 'outline'}
              disabled={decidir.isPending || justificativa.trim().length < JUSTIFICATIVA_MIN}
              onClick={() => decidir.mutate(opcao.value)}
            >
              {opcao.label}
            </Button>
          ))}
        </div>
      ) : null}
    </li>
  )
}

/**
 * Revisão humana: cada item duvidoso recebe uma decisão com justificativa; o texto é corrigido e a
 * pessoa declara que revisou. Só então a peça pode ser exportada (a API também impede antes disso).
 */
export function RevisaoPeca({ peca, onAtualizar }: { peca: PecaDetalhe; onAtualizar: () => void }) {
  const [texto, setTexto] = useState(peca.textoRevisado ?? peca.textoGerado ?? '')
  const [declaro, setDeclaro] = useState(false)
  const citacoes = peca.citacoes ?? []
  const pendentes = citacoes.filter((c) => c.bloqueante && c.decisao === 'pendente').length

  const concluir = useMutation({
    mutationFn: () => concluirRevisao(peca.id, texto),
    onSuccess: onAtualizar,
    onError: (error) => handleApiError(error),
  })
  const exportar = useMutation({
    mutationFn: async (formato: 'docx' | 'md') => ({
      formato,
      conteudo: await exportarPeca(peca.id, formato),
    }),
    onSuccess: ({ formato, conteudo }) => {
      const url = URL.createObjectURL(conteudo)
      const ancora = document.createElement('a')
      ancora.href = url
      ancora.download = `${peca.tipoPeca}-v${peca.versao}.${formato}`
      ancora.click()
      URL.revokeObjectURL(url)
    },
    onError: (error) => handleApiError(error),
  })

  if (peca.revisadoEm) {
    return (
      <section aria-label="Peça revisada" className="space-y-3 border-t pt-4">
        <p className="flex items-center gap-2 text-sm font-medium">
          <CheckCircle2 className="size-4" aria-hidden />
          Revisão concluída em {new Date(peca.revisadoEm).toLocaleString('pt-BR')}
        </p>
        <div className="flex flex-wrap gap-3">
          <Button
            type="button"
            disabled={exportar.isPending}
            onClick={() => exportar.mutate('docx')}
          >
            <Download aria-hidden />
            Exportar Word (.docx)
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={exportar.isPending}
            onClick={() => exportar.mutate('md')}
          >
            <Download aria-hidden />
            Exportar Markdown
          </Button>
        </div>
      </section>
    )
  }

  return (
    <section aria-labelledby="revisao-titulo" className="space-y-4 border-t pt-4">
      <div className="space-y-1">
        <h2 id="revisao-titulo" className="text-sm font-medium">
          Revisão humana
        </h2>
        <p className="text-xs text-muted-foreground">
          A exportação só é liberada depois que você decidir cada item pendente e declarar que
          revisou a minuta.
        </p>
      </div>

      {citacoes.length > 0 ? (
        <ul className="divide-y">
          {citacoes.map((citacao) => (
            <ItemCitacao
              key={citacao.id}
              pecaId={peca.id}
              citacao={citacao}
              onDecidida={onAtualizar}
            />
          ))}
        </ul>
      ) : null}

      <div className="space-y-1.5">
        <label htmlFor="texto-final" className="text-sm font-medium">
          Texto final
        </label>
        <Textarea
          id="texto-final"
          rows={14}
          value={texto}
          onChange={(evento) => setTexto(evento.target.value)}
        />
      </div>

      <label className="flex items-start gap-2 text-sm">
        <Checkbox
          checked={declaro}
          onCheckedChange={(marcado) => setDeclaro(marcado === true)}
          className="mt-0.5"
        />
        Declaro que li e conferi a minuta, as citações e os fatos, e assumo a responsabilidade pelo
        texto final.
      </label>

      <div className="flex flex-wrap items-center gap-3">
        <Button
          type="button"
          disabled={!declaro || pendentes > 0 || texto.trim() === '' || concluir.isPending}
          onClick={() => concluir.mutate()}
        >
          {concluir.isPending ? <LoadingSpinner size="sm" decorative /> : null}
          Concluir revisão
        </Button>
        {pendentes > 0 ? (
          <p className="text-xs text-muted-foreground">
            {pendentes === 1 ? 'Falta decidir 1 item.' : `Faltam decidir ${pendentes} itens.`}
          </p>
        ) : null}
      </div>
    </section>
  )
}

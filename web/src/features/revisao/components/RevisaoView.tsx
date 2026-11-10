'use client'

import { CheckCircle2, Sparkles, X } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { ToneOutlineButton } from '@/components/shared/ToneOutlineButton'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { useUiStore } from '@/stores/uiStore'
import { addBusinessDays } from '../diasUteis'
import { proximoNaoDecidido, rotuloDecisao, type ItemRevisao, type Resultado } from '../fila'
import { useDecidirRevisao, useFilaRevisao } from '../hooks'
import { useRevisaoStore } from '../store'

const variantes = [
  { value: 'A', label: 'Variação A' },
  { value: 'B', label: 'Variação B' },
] as const

const nn = (indice: number) => String(indice + 1).padStart(2, '0')

/** Texto da publicação com o trecho que gerou o prazo destacado (`<mark>`). */
function TextoComTrecho({ item, className }: { item: ItemRevisao; className?: string }) {
  const { content } = item.publication
  const { trecho } = item.sugestao

  return (
    <p className={className}>
      {trecho ? (
        <>
          {content.slice(0, trecho.inicio)}
          <mark className="border-b-2 border-ia bg-ia-soft px-0.5 text-foreground">
            {content.slice(trecho.inicio, trecho.fim)}
          </mark>
          {content.slice(trecho.fim)}
        </>
      ) : (
        content
      )}
    </p>
  )
}

interface DecisaoProps {
  item: ItemRevisao
  resultado: Resultado | undefined
  pending: boolean
  onDecidir: (decisao: 'accepted' | 'corrected' | 'rejected', dias: number) => void
  /** `bar`: barra de decisão da variação B; `card`: botões grandes do cartão da variação A. */
  layout: 'card' | 'bar'
}

/**
 * Aceitar / Corrigir / Rejeitar. Botões CONTORNADOS (nunca um "principal" sólido que induza ao
 * clique). Corrigir troca o prazo por um campo numérico e o botão vira Confirmar. Decidido, os
 * botões dão lugar ao resultado.
 */
function Decisao({ item, resultado, pending, onDecidir, layout }: DecisaoProps) {
  const [corrigindo, setCorrigindo] = useState(false)
  const [dias, setDias] = useState(String(item.sugestao.dias))
  const valor = Number(dias)
  // `Number('')` é 0: sem exigir texto, um campo vazio salvaria um prazo de 0 dias.
  const valido = dias.trim() !== '' && Number.isInteger(valor) && valor >= 0 && valor <= 365
  const grande = layout === 'card' ? 'h-[54px] flex-1 text-base' : 'h-10'

  if (resultado) {
    return (
      <p
        role="status"
        className="flex items-center gap-2 rounded-[10px] border border-success/30 bg-success-soft px-4 py-3 text-sm font-medium text-success-foreground"
      >
        <CheckCircle2 className="size-4 shrink-0" aria-hidden />
        {rotuloDecisao[resultado.decisao]}
      </p>
    )
  }

  return (
    <div className="space-y-3">
      {corrigindo ? (
        <div className="flex items-center gap-2">
          <label htmlFor="prazo-corrigido" className="text-sm font-medium">
            Prazo em dias úteis
          </label>
          <Input
            id="prazo-corrigido"
            type="number"
            inputMode="numeric"
            min={0}
            max={365}
            value={dias}
            onChange={(event) => setDias(event.target.value)}
            aria-invalid={!valido}
            className="w-24 border-ia font-mono"
            autoFocus
          />
          {!valido ? (
            <span className="text-xs text-destructive-foreground">
              Use um número inteiro de 0 a 365.
            </span>
          ) : null}
        </div>
      ) : null}

      <div className={cn('flex gap-3', layout === 'bar' && 'justify-end')}>
        <ToneOutlineButton
          tone="danger"
          className={grande}
          disabled={pending}
          onClick={() => onDecidir('rejected', item.sugestao.dias)}
        >
          Rejeitar
        </ToneOutlineButton>
        {corrigindo ? (
          <ToneOutlineButton
            tone="ia"
            className={grande}
            disabled={pending || !valido}
            onClick={() => onDecidir('corrected', valor)}
          >
            Confirmar
          </ToneOutlineButton>
        ) : (
          <ToneOutlineButton
            tone="ia"
            className={grande}
            disabled={pending}
            onClick={() => setCorrigindo(true)}
          >
            {layout === 'bar' ? 'Corrigir prazo' : 'Corrigir'}
          </ToneOutlineButton>
        )}
        <ToneOutlineButton
          tone="ok"
          className={grande}
          disabled={pending || corrigindo}
          onClick={() => onDecidir('accepted', item.sugestao.dias)}
        >
          Aceitar
        </ToneOutlineButton>
      </div>
    </div>
  )
}

function Vencimento({ item }: { item: ItemRevisao }) {
  const data = addBusinessDays(new Date(item.publication.publishedAt), item.sugestao.dias)

  return (
    <>
      <time dateTime={data.toISOString()} className="font-mono">
        {formatDate(data)}
      </time>
      <span className="block text-xs font-normal text-muted-foreground">
        estimativa — não considera feriados nem suspensões
      </span>
    </>
  )
}

function Fundamento({ item }: { item: ItemRevisao }) {
  return <span>{item.sugestao.fundamento}</span>
}

function PainelA({ item, resultado, pending, onDecidir }: Omit<DecisaoProps, 'layout'>) {
  const [dicaFechada, setDicaFechada] = useState(false)
  const { publication, sugestao } = item

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <section
        aria-label="Texto da publicação"
        className="space-y-3 rounded-[10px] border bg-card p-5 shadow-sm"
      >
        <p className="text-sm text-muted-foreground">
          {publication.tribunal ?? publication.journal}
          {publication.processNumber ? ` · ` : ''}
          <span className="font-mono">{publication.processNumber}</span>
          {publication.clienteNome ? ` · ${publication.clienteNome}` : ''}
        </p>
        <TextoComTrecho item={item} className="text-base leading-[1.75]" />
      </section>

      <section
        aria-label="Sugestão da IA"
        className="space-y-4 rounded-[10px] border border-t-2 border-t-ia bg-card p-5 shadow-sm"
      >
        <p className="flex items-center gap-2 text-sm font-medium text-ia-text">
          <Sparkles className="size-4" aria-hidden />
          Sugestão da IA
          <span className="ml-auto font-mono text-xs text-muted-foreground">
            confiança {sugestao.confianca}%
          </span>
        </p>

        {dicaFechada ? null : (
          <p className="flex items-start gap-2 rounded-lg bg-ia-soft p-3 text-sm">
            <span className="flex-1">
              A IA leu esta publicação e sugeriu um prazo. Revise antes de salvar.
            </span>
            <button
              type="button"
              onClick={() => setDicaFechada(true)}
              aria-label="Dispensar dica"
              className="rounded-sm p-0.5 text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
            >
              <X className="size-3.5" aria-hidden />
            </button>
          </p>
        )}

        <dl className="grid grid-cols-[110px_1fr] gap-x-3 gap-y-3 text-sm">
          <dt className="text-muted-foreground">Tipo</dt>
          <dd className="font-medium">{sugestao.tipo}</dd>
          <dt className="text-muted-foreground">Prazo</dt>
          <dd className="font-medium">{sugestao.dias} dias úteis</dd>
          <dt className="text-muted-foreground">Vencimento</dt>
          <dd className="font-medium">
            <Vencimento item={item} />
          </dd>
          <dt className="text-muted-foreground">Fundamento legal</dt>
          <dd>
            <Fundamento item={item} />
          </dd>
        </dl>

        <Decisao
          key={publication.id}
          item={item}
          resultado={resultado}
          pending={pending}
          onDecidir={onDecidir}
          layout="card"
        />
      </section>
    </div>
  )
}

function PainelB({ item, resultado, pending, onDecidir }: Omit<DecisaoProps, 'layout'>) {
  const { publication, sugestao } = item
  const metricas = [
    ['Tipo sugerido', sugestao.tipo],
    ['Prazo em dias úteis', `${sugestao.dias}`],
    ['Vence em', null],
    ['Confiança', `${sugestao.confianca}%`],
  ] as const

  return (
    <div className="space-y-6">
      <section aria-label="Resumo da sugestão" className="grid grid-cols-2 divide-x md:grid-cols-4">
        {metricas.map(([rotulo, valor]) => (
          <div key={rotulo} className="space-y-1 px-5 first:pl-0">
            <p className="text-sm text-muted-foreground">{rotulo}</p>
            <p className="text-[22px] leading-tight font-medium">
              {valor ?? <Vencimento item={item} />}
            </p>
          </div>
        ))}
      </section>

      <section aria-label="Texto da publicação" className="max-w-3xl space-y-3">
        <p className="text-sm text-muted-foreground">
          {publication.tribunal ?? publication.journal} ·{' '}
          <span className="font-mono">{publication.processNumber}</span>
        </p>
        <TextoComTrecho item={item} className="text-[17px] leading-[1.85]" />
        <p className="border-t pt-3 text-sm text-muted-foreground">
          <sup>1</sup> · <span className="font-medium text-ia-text">Athena</span>:{' '}
          <Fundamento item={item} />
        </p>
      </section>

      {/* Barra de decisão: fica grudada na base enquanto o texto rola. */}
      <div className="sticky bottom-0 -mx-4 border-t bg-background/95 px-4 py-3 backdrop-blur sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <Decisao
            key={publication.id}
            item={item}
            resultado={resultado}
            pending={pending}
            onDecidir={onDecidir}
            layout="bar"
          />
        </div>
      </div>
    </div>
  )
}

/** Sem mocks não há sugestão da IA por publicação: a tela diz isso em vez de inventar um prazo. */
function SemSugestoes() {
  return (
    <EmptyState
      icon={Sparkles}
      title="A revisão da IA ainda não está disponível"
      description="A API não informa, por publicação, o que a IA sugeriu (tipo, prazo e fundamento). Enquanto isso, trate as publicações pela lista."
      action={
        <Link
          href={routes.publications}
          className="text-sm font-medium underline underline-offset-4"
        >
          Ir para as publicações
        </Link>
      }
    />
  )
}

export function RevisaoView() {
  const demo = env.useMocks
  const variant = useUiStore((s) => s.reviewVariant)
  const setVariant = useUiStore((s) => s.setReviewVariant)
  const { itens, isPending, isError, refetch } = useFilaRevisao(demo)
  const decisoes = useRevisaoStore((s) => s.decisoes)
  const [escolhido, setEscolhido] = useState<string | null>(null)

  const decidir = useDecidirRevisao((id) => {
    // Lê o estado já atualizado (o registro acontece antes deste callback).
    setEscolhido(proximoNaoDecidido(itens, useRevisaoStore.getState().decisoes, id))
  })

  const pendentes = itens.filter((item) => !decisoes[item.publication.id]).length
  const ativo = itens.find((item) => item.publication.id === escolhido) ?? itens[0]
  // Componente (não chamada de função): cada variação tem os próprios hooks.
  const Painel = variant === 'A' ? PainelA : PainelB

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="space-y-1">
          <p className="text-xs font-medium text-muted-foreground">
            Revisão humana obrigatória{demo ? ` · ${pendentes} pendentes` : ''}
          </p>
          <h1 className="text-[33px] leading-tight font-semibold">Revisão da IA</h1>
        </div>
        {demo ? (
          <SegmentedControl
            value={variant}
            onChange={setVariant}
            options={variantes}
            label="Variação da tela de revisão"
          />
        ) : null}
      </header>

      {!demo ? (
        <SemSugestoes />
      ) : (
        <>
          <p
            role="note"
            className="rounded-lg border border-dashed px-4 py-2 text-sm text-muted-foreground"
          >
            <strong className="font-medium text-foreground">Sugestões de demonstração.</strong> A
            API ainda não informa a sugestão da IA por publicação: o prazo abaixo é calculado a
            partir do texto e ignora feriados. As decisões gravam o status da publicação e o prazo
            na agenda.
          </p>

          {isError ? (
            <ErrorState
              error={new Error('Falha ao carregar as publicações.')}
              title="Não foi possível carregar a fila de revisão"
              onRetry={() => void refetch()}
            />
          ) : isPending ? (
            <div className="space-y-3" role="status" aria-label="Carregando a revisão">
              <Skeleton className="h-10 w-2/3" />
              <Skeleton className="h-64" style={{ animationDelay: '0.15s' }} />
            </div>
          ) : itens.length === 0 ? (
            <EmptyState
              icon={CheckCircle2}
              title="Nenhuma publicação aguardando revisão"
              description="Quando houver publicações com processo vinculado, elas aparecem aqui."
            />
          ) : (
            <>
              <div
                role="tablist"
                aria-label="Publicações a revisar"
                className="flex gap-2 overflow-x-auto pb-1"
              >
                {itens.map((item, indice) => {
                  const id = item.publication.id
                  const decidida = Boolean(decisoes[id])
                  const selecionada = ativo?.publication.id === id

                  return (
                    <button
                      key={id}
                      type="button"
                      role="tab"
                      id={`aba-${id}`}
                      aria-selected={selecionada}
                      aria-controls="painel-revisao"
                      tabIndex={selecionada ? 0 : -1}
                      onClick={() => setEscolhido(id)}
                      className={cn(
                        'shrink-0 rounded-full border px-3.5 py-1.5 text-sm font-medium whitespace-nowrap outline-none focus-visible:ring-2 focus-visible:ring-ring',
                        selecionada
                          ? 'border-ia bg-ia-soft text-ia-text'
                          : 'text-muted-foreground hover:bg-accent hover:text-foreground'
                      )}
                    >
                      <span className="font-mono text-xs">{nn(indice)}</span> {item.sugestao.tipo}
                      {decidida ? (
                        <>
                          {' '}
                          <span aria-hidden>✓</span>
                          <span className="sr-only">(decidida)</span>
                        </>
                      ) : null}
                    </button>
                  )
                })}
              </div>

              {ativo ? (
                <div
                  id="painel-revisao"
                  role="tabpanel"
                  aria-labelledby={`aba-${ativo.publication.id}`}
                >
                  <Painel
                    item={ativo}
                    resultado={decisoes[ativo.publication.id]}
                    pending={decidir.isPending}
                    onDecidir={(decisao, dias) => decidir.mutate({ item: ativo, decisao, dias })}
                  />
                </div>
              ) : null}

              {variant === 'A' ? (
                <p className="text-center text-sm text-muted-foreground">
                  Responsabilidade final do advogado — nada é salvo sem sua decisão.
                </p>
              ) : null}
            </>
          )}
        </>
      )}
    </div>
  )
}

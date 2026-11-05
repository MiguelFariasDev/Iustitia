'use client'

import { Check, Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { cn } from '@/lib/utils'

/**
 * Demonstração do hero: a tela de Revisão da IA, montada em HTML/CSS com DADOS FICTÍCIOS (a
 * legenda "Exemplo ilustrativo" é obrigatória). Ciclo curto e discreto:
 *   0 texto da publicação → 1 trecho gerador pisca (violeta) → 2 sugestão aparece → 3 hover
 *   simulado em "Aceitar" → 4 toast "Prazo aceito". Reinicia após ~8 s e PAUSA no hover/foco.
 * Com `prefers-reduced-motion` mostra só o estado final, estático. Só opacity/transform.
 */
const STAGE_MS = [1200, 1400, 1800, 1400, 2200] as const // soma ≈ 8 s

export function HeroDemo() {
  const [stage, setStage] = useState(0)
  const [paused, setPaused] = useState(false)
  const [reduced, setReduced] = useState(false)

  useEffect(() => {
    const query = window.matchMedia('(prefers-reduced-motion: reduce)')
    const sync = () => setReduced(query.matches)
    sync()
    query.addEventListener('change', sync)
    return () => query.removeEventListener('change', sync)
  }, [])

  useEffect(() => {
    if (reduced || paused) return
    const timer = setTimeout(() => setStage((s) => (s + 1) % STAGE_MS.length), STAGE_MS[stage])
    return () => clearTimeout(timer)
  }, [stage, paused, reduced])

  // Reduzido: estado final estático (sugestão visível e aceita), sem toast flutuante.
  const s = reduced ? 3 : stage
  const showSuggestion = s >= 2
  const accepting = s >= 3
  const flashing = s === 1 && !reduced

  return (
    <figure
      className="iu-card relative overflow-hidden p-0 shadow-lg"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocusCapture={() => setPaused(true)}
      onBlurCapture={() => setPaused(false)}
      aria-label="Exemplo ilustrativo da tela de revisão da Athena"
    >
      <div className="flex items-center justify-between border-b px-4 py-2.5 text-xs text-muted-foreground">
        <span className="iu-mono">0001234-56.2026.8.26.0100</span>
        <span>Revisão da IA</span>
      </div>

      <div className="grid gap-px bg-border sm:grid-cols-2">
        <div className="bg-card p-4">
          <p className="mb-2 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
            Publicação
          </p>
          <p className="text-sm leading-relaxed">
            Vistos. Cite-se a parte ré para{' '}
            <mark
              className={cn(
                'rounded px-0.5 text-foreground',
                'bg-[var(--iu-accent-soft)] decoration-[var(--iu-accent)]',
                flashing && 'iu-pulse'
              )}
            >
              oferecer contestação no prazo legal
            </mark>
            , sob pena de revelia.
          </p>
        </div>

        <div className="bg-card p-4">
          <p className="mb-2 flex items-center gap-1.5 text-[11px] font-medium tracking-wide text-[var(--iu-accent)] uppercase">
            <Sparkles className="size-3" aria-hidden /> Athena sugere
          </p>
          {showSuggestion ? (
            <div key="suggestion" className="iu-in space-y-3">
              <div>
                <p className="text-sm font-semibold">Contestação</p>
                <p className="text-sm text-muted-foreground">
                  15 dias úteis → <span className="iu-mono text-foreground">26/11/2026</span>
                </p>
                <p className="mt-1 text-xs text-muted-foreground">Confiança 94% · CPC, art. 335</p>
              </div>
              <div className="flex flex-wrap gap-2" aria-hidden>
                <span
                  className={cn(
                    'rounded-[10px] border-[1.5px] px-3 py-1.5 text-xs font-medium transition-colors',
                    accepting
                      ? 'border-[var(--iu-brand)] bg-[var(--iu-brand-soft)] text-[var(--iu-brand)]'
                      : 'border-border'
                  )}
                >
                  {accepting ? <Check className="mr-1 inline size-3" /> : null}Aceitar
                </span>
                <span className="rounded-[10px] border-[1.5px] px-3 py-1.5 text-xs">Corrigir</span>
                <span className="rounded-[10px] border-[1.5px] px-3 py-1.5 text-xs">Rejeitar</span>
              </div>
            </div>
          ) : (
            <div className="space-y-2" aria-hidden>
              <div className="iu-pulse h-3 w-2/3 rounded bg-muted" />
              <div className="iu-pulse h-3 w-1/2 rounded bg-muted" />
            </div>
          )}
        </div>
      </div>

      {s === 4 && !reduced ? (
        <div
          role="status"
          className="iu-in iu-center-x absolute bottom-3 rounded-[10px] bg-[var(--iu-side)] px-3 py-2 text-xs text-white shadow-lg"
        >
          Prazo aceito — adicionado à agenda
        </div>
      ) : null}

      <figcaption className="border-t px-4 py-2 text-[11px] text-muted-foreground">
        Exemplo ilustrativo · dados fictícios
      </figcaption>
    </figure>
  )
}

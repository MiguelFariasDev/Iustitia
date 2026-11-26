export type Coluna = 'afazer' | 'andamento' | 'revisao' | 'concluido'

export const colunas: readonly { id: Coluna; label: string }[] = [
  { id: 'afazer', label: 'A fazer' },
  { id: 'andamento', label: 'Em andamento' },
  { id: 'revisao', label: 'Em revisão' },
  { id: 'concluido', label: 'Concluído' },
]

export interface Tarefa {
  id: string
  titulo: string
  /** Número do processo (mono na tela); vazio = tarefa sem processo. */
  processo: string
  /** Id do processo, quando a tarefa foi criada sobre um. */
  processoId?: string | null
  /** Nome de quem responde pela tarefa; vazio = sem responsável. */
  responsavel: string
  responsavelId?: string | null
  coluna: Coluna
  /** ISO yyyy-MM-dd do prazo; `null` = sem prazo. */
  prazo: string | null
  /** ISO de quando a tarefa foi criada: o início da contagem do "% do prazo decorrido". */
  criadaEm: string
}

export const rotuloColuna = (coluna: Coluna) => colunas.find((c) => c.id === coluna)!.label

/** Coluna vizinha (-1 = esquerda, +1 = direita), ou `null` nas pontas. */
export function colunaVizinha(coluna: Coluna, passo: -1 | 1): Coluna | null {
  const indice = colunas.findIndex((c) => c.id === coluna) + passo

  return colunas[indice]?.id ?? null
}

/** Move a tarefa. Não muda a lista original; id inexistente ou mesma coluna devolvem a lista como está. */
export function moverTarefa(tarefas: readonly Tarefa[], id: string, destino: Coluna): Tarefa[] {
  return tarefas.map((tarefa) => (tarefa.id === id ? { ...tarefa, coluna: destino } : tarefa))
}

export type TomPrazo = 'ok' | 'atencao' | 'critico'

export interface Progresso {
  /** 0–100: quanto do tempo entre a criação e o prazo já passou. */
  pct: number
  tom: TomPrazo
  /** Rótulo central do anel: "2d", "0d", "1s" ou "ok" (concluída). */
  rotulo: string
}

/** Circunferência do anel (r = 15): 2π·15. */
export const CIRCUNFERENCIA = 94.2

/** `stroke-dashoffset` do anel para `pct` por cento preenchido. */
export const deslocamento = (pct: number) => CIRCUNFERENCIA * (1 - pct / 100)

const DIA = 24 * 60 * 60 * 1000

function inicioDoDia(data: Date) {
  return new Date(data.getFullYear(), data.getMonth(), data.getDate()).getTime()
}

/** Interpreta yyyy-MM-dd como dia LOCAL (new Date('2026-11-05') seria meia-noite UTC: dia anterior no Brasil). */
function diaLocal(iso: string): Date {
  const [ano, mes, dia] = iso.split('-').map(Number)

  return new Date(ano!, mes! - 1, dia)
}

/**
 * Atrasada: não concluída e com prazo ANTES de hoje (vencer hoje ainda não é atraso).
 * Tarefa sem prazo nunca atrasa.
 */
export function estaAtrasada(tarefa: Tarefa, agora: Date): boolean {
  if (tarefa.coluna === 'concluido' || !tarefa.prazo) return false

  return diaLocal(tarefa.prazo).getTime() < inicioDoDia(agora)
}

/** Dias de atraso (1 = venceu ontem); 0 se não está atrasada. */
export function diasDeAtraso(tarefa: Tarefa, agora: Date): number {
  if (!estaAtrasada(tarefa, agora)) return 0

  return Math.round((inicioDoDia(agora) - diaLocal(tarefa.prazo!).getTime()) / DIA)
}

/** Atrasadas, da mais antiga para a mais recente. */
export function tarefasAtrasadas(tarefas: readonly Tarefa[], agora: Date): Tarefa[] {
  return tarefas
    .filter((tarefa) => estaAtrasada(tarefa, agora))
    .sort((a, b) => a.prazo!.localeCompare(b.prazo!))
}

/**
 * Progresso do prazo. Cor: a partir de 90% vermelho, a partir de 70% violeta, antes disso verde.
 * Concluída sempre mostra "ok" e verde, qualquer que seja o prazo. Sem prazo: 0% e "—".
 * Prazo vencido: 100%, vermelho, rótulo negativo ("-3d").
 */
export function progresso(tarefa: Tarefa, agora: Date): Progresso {
  if (tarefa.coluna === 'concluido') return { pct: 100, tom: 'ok', rotulo: 'ok' }
  if (!tarefa.prazo) return { pct: 0, tom: 'ok', rotulo: '—' }

  const prazo = diaLocal(tarefa.prazo).getTime()
  const inicio = inicioDoDia(new Date(tarefa.criadaEm))
  const hoje = inicioDoDia(agora)
  const total = Math.max(prazo - inicio, DIA)
  const pct = Math.min(100, Math.max(0, Math.round(((hoje - inicio) / total) * 100)))
  const diasRestantes = Math.round((prazo - hoje) / DIA)

  const tom: TomPrazo = pct >= 90 ? 'critico' : pct >= 70 ? 'atencao' : 'ok'
  const rotulo =
    diasRestantes < 0
      ? `${diasRestantes}d`
      : diasRestantes >= 7
        ? `${Math.floor(diasRestantes / 7)}s`
        : `${diasRestantes}d`

  return { pct: diasRestantes < 0 ? 100 : pct, tom: diasRestantes < 0 ? 'critico' : tom, rotulo }
}

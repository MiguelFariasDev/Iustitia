import type { AgendaEvento } from '@/features/agenda/types'
import { formatHora } from '@/features/agenda/dates'

/**
 * Respostas da Athena em DEMONSTRAÇÃO. Não existe serviço de IA ligado: nada aqui finge
 * inteligência. "Prazos de hoje" é calculado com os dados REAIS da agenda; o resto diz, com
 * clareza, que a IA não está conectada neste ambiente. Tom da casa: "eu sugiro, você decide".
 */
export const RESPOSTA_SEM_IA =
  'Neste ambiente de demonstração eu ainda não estou conectada ao serviço de IA, então não consigo ler ' +
  'suas publicações nem seus processos. Quando eu estiver, eu sugiro e você decide — nada é salvo sem a sua revisão.'

export function respostaPrazosDeHoje(prazos: readonly AgendaEvento[]): string {
  if (prazos.length === 0) return 'Não há prazos marcados para hoje na sua agenda.'

  const linhas = prazos.map((prazo) =>
    prazo.diaInteiro
      ? `• ${prazo.titulo} (dia todo)`
      : `• ${prazo.titulo}, às ${formatHora(prazo.inicio)}`
  )
  const quantidade =
    prazos.length === 1 ? '1 prazo vence hoje' : `${prazos.length} prazos vencem hoje`

  return `${quantidade}:\n${linhas.join('\n')}\nEu sugiro começar pelo mais cedo; você decide a ordem.`
}

export const CHIPS = ['Prazos de hoje', 'Resumir publicação'] as const
export type Chip = (typeof CHIPS)[number]

export function saudacaoInicial(nome: string): string {
  const quem = nome ? `Olá, ${nome}.` : 'Olá.'

  return `${quem} Sou a Athena, assistente jurídica do escritório. Eu sugiro, você decide. Por onde começamos?`
}

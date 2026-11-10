import type { Publication, PublicationKind } from '@/features/publicacoes/types'

/**
 * SUGESTÃO DE DEMONSTRAÇÃO. A API não expõe, por publicação, o que a IA sugeriu (tipo, prazo,
 * fundamento, confiança). Em modo de demonstração (mocks) esta função monta uma sugestão
 * determinística a partir do texto e do tipo da publicação, para a tela de revisão poder ser
 * exercitada. Sem mocks a tela NÃO chama isto: não se inventa prazo para um escritório real.
 */
export interface Sugestao {
  /** Providência sugerida (ex.: "Contestação"). */
  tipo: string
  /** Prazo em dias úteis. */
  dias: number
  /** Fundamento legal, em texto curto. */
  fundamento: string
  /** 0–100. */
  confianca: number
  /** Trecho do texto que gerou o prazo (destacado na tela); `null` se não foi localizado. */
  trecho: { inicio: number; fim: number } | null
}

const porTipo: Record<PublicationKind, { tipo: string; dias: number; fundamento: string }> = {
  citation: { tipo: 'Contestação', dias: 15, fundamento: 'CPC, art. 335 (15 dias úteis)' },
  intimation: {
    tipo: 'Manifestação',
    dias: 5,
    fundamento: 'CPC, art. 218, § 3º (5 dias, quando a lei ou o juiz não fixar outro)',
  },
  decision: {
    tipo: 'Embargos de declaração',
    dias: 5,
    fundamento: 'CPC, art. 1.023 (5 dias)',
  },
  sentence: {
    tipo: 'Recurso de apelação',
    dias: 15,
    fundamento: 'CPC, art. 1.003, § 5º (15 dias úteis)',
  },
  hearing: { tipo: 'Comparecimento à audiência', dias: 0, fundamento: 'Data designada na decisão' },
  other: { tipo: 'Providência', dias: 5, fundamento: 'CPC, art. 218, § 3º' },
}

/** "15 (quinze) dias", "5 (cinco) dias úteis" — o prazo escrito na própria publicação. */
const PRAZO_NO_TEXTO = /(\d{1,3})\s*\([^)]*\)\s*dias(?:\s+úteis)?/i

/** Hash simples e estável: a mesma publicação sempre recebe a mesma confiança na demonstração. */
function hash(texto: string): number {
  let h = 0
  for (const caractere of texto) h = (h * 31 + caractere.charCodeAt(0)) >>> 0

  return h
}

export function sugerirDemo(publication: Pick<Publication, 'id' | 'kind' | 'content'>): Sugestao {
  const base = porTipo[publication.kind]
  const achado = PRAZO_NO_TEXTO.exec(publication.content)

  // Prazo escrito no texto vence a tabela por tipo: é o que o juiz de fato fixou.
  const dias = achado ? Number(achado[1]) : base.dias
  // Com o prazo no texto a confiança é alta; sem ele, a IA estaria adivinhando pelo tipo.
  const faixa = achado ? 88 : 72

  return {
    tipo: base.tipo,
    dias,
    // Prazo lido do texto: cita só a origem. Colar o artigo da tabela por tipo aqui poderia
    // contradizer o prazo (ex.: art. de 5 dias ao lado de um prazo de 15), e isso é pior que não citar.
    fundamento: achado ? 'Prazo fixado no próprio texto da publicação' : base.fundamento,
    confianca: faixa + (hash(publication.id) % 10),
    trecho: achado ? { inicio: achado.index, fim: achado.index + achado[0].length } : null,
  }
}

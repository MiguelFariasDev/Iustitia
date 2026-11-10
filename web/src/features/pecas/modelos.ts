/**
 * MODELOS DE DEMONSTRAÇÃO. Não existe API de geração de peças: em modo de demonstração (mocks)
 * estes modelos montam um ESQUELETO da peça — estrutura e artigos de lei conferidos — com as
 * lacunas `[...]` a preencher pelo advogado. Não é texto jurídico pronto, e a tela diz isso.
 * Sem mocks a tela não usa nada daqui: não se gera peça de aparência pronta para um escritório real.
 */
export type TipoPeca = 'contestacao' | 'replica' | 'apelacao' | 'contrarrazoes'

export const tiposPeca: readonly { value: TipoPeca; label: string }[] = [
  { value: 'contestacao', label: 'Contestação' },
  { value: 'replica', label: 'Réplica à contestação' },
  { value: 'apelacao', label: 'Recurso de apelação' },
  { value: 'contrarrazoes', label: 'Contrarrazões' },
]

export interface DadosProcesso {
  cnjNumber: string
  tribunal: string | null
  comarca: string | null
  vara: string | null
  parteContraria: string | null
}

export interface Minuta {
  tipo: TipoPeca
  titulo: string
  paragrafos: string[]
}

interface Modelo {
  /** Nome da peça no corpo do texto (maiúsculas). */
  nome: string
  /** Fundamento da peça, citado na abertura. */
  fundamento: string
  /** Dispositivo e prazo, citados no item de tempestividade. */
  prazo: string
  fatos: string
  direito: string
  pedidos: string
}

const modelos: Record<TipoPeca, Modelo> = {
  contestacao: {
    nome: 'CONTESTAÇÃO',
    fundamento: 'art. 335 do CPC',
    prazo: 'prazo de 15 (quinze) dias úteis previsto no art. 335 do CPC',
    fatos: '[Descrever os fatos conforme os documentos do processo.]',
    direito:
      '[Fundamentar a defesa. Impugnar especificamente os fatos alegados, nos termos do art. 341 do CPC, e arguir as preliminares cabíveis (art. 337 do CPC).]',
    pedidos:
      'Ante o exposto, requer o acolhimento das preliminares, se houver, e, no mérito, a total improcedência dos pedidos [ajustar], com a condenação da parte adversa nos ônus da sucumbência.',
  },
  replica: {
    nome: 'RÉPLICA À CONTESTAÇÃO',
    fundamento: 'art. 351 do CPC',
    prazo: 'prazo de 15 (quinze) dias úteis previsto no art. 351 do CPC',
    fatos: '[Resumir os pontos da contestação a serem rebatidos.]',
    direito:
      '[Rebater as preliminares e os fatos novos alegados na defesa; requerer a produção das provas pertinentes (art. 369 do CPC).]',
    pedidos:
      'Ante o exposto, requer a rejeição das preliminares e dos argumentos da contestação [ajustar], com o prosseguimento do feito e o julgamento de procedência dos pedidos iniciais.',
  },
  apelacao: {
    nome: 'RECURSO DE APELAÇÃO',
    fundamento: 'art. 1.009 do CPC',
    prazo: 'prazo de 15 (quinze) dias úteis previsto no art. 1.003, § 5º, do CPC',
    fatos: '[Relatar o processo e a sentença recorrida.]',
    direito:
      '[Expor as razões do pedido de reforma ou anulação, com a exposição do fato e do direito e os fundamentos da nova decisão (art. 1.010 do CPC).]',
    pedidos:
      'Ante o exposto, requer o conhecimento e o provimento do recurso para reformar [ou anular] a sentença, nos termos acima, invertendo-se os ônus da sucumbência.',
  },
  contrarrazoes: {
    nome: 'CONTRARRAZÕES DE APELAÇÃO',
    fundamento: 'art. 1.010, § 1º, do CPC',
    prazo: 'prazo de 15 (quinze) dias úteis previsto no art. 1.010, § 1º, do CPC',
    fatos: '[Resumir a sentença e as razões do apelante.]',
    direito: '[Demonstrar o acerto da sentença e rebater cada fundamento do recurso.]',
    pedidos:
      'Ante o exposto, requer o não provimento do recurso, mantendo-se integralmente a sentença, com a majoração dos honorários advocatícios (art. 85, § 11, do CPC).',
  },
}

export function montarMinuta(tipo: TipoPeca, processo: DadosProcesso): Minuta {
  const modelo = modelos[tipo]
  const vara = processo.vara ?? '[VARA]'
  const comarca = processo.comarca ?? processo.tribunal ?? '[COMARCA]'
  const adversa = processo.parteContraria ?? '[PARTE ADVERSA]'

  return {
    tipo,
    titulo: modelo.nome,
    paragrafos: [
      `EXCELENTÍSSIMO(A) SENHOR(A) DOUTOR(A) JUIZ(A) DE DIREITO DA ${vara} DE ${comarca}`,
      `Processo nº ${processo.cnjNumber}`,
      `[NOME DO CLIENTE], já qualificado(a) nos autos do processo em epígrafe, em face de ${adversa}, vem, respeitosamente, por seu advogado, apresentar ${modelo.nome}, com fundamento no ${modelo.fundamento}, pelas razões de fato e de direito a seguir expostas.`,
      `I – DA TEMPESTIVIDADE. A presente peça é apresentada dentro do ${modelo.prazo}. [Confirmar a data de início da contagem e eventuais feriados ou suspensões.]`,
      `II – DOS FATOS. ${modelo.fatos}`,
      `III – DO DIREITO. ${modelo.direito}`,
      `IV – DOS PEDIDOS. ${modelo.pedidos}`,
      'Nestes termos, pede deferimento. [Local], [data]. [ADVOGADO(A) — OAB/UF]',
    ],
  }
}

/** Texto corrido da minuta, para salvar como arquivo. */
export function minutaComoTexto(minuta: Minuta): string {
  return `${minuta.titulo}\n\n${minuta.paragrafos.join('\n\n')}\n`
}

export function nomeDoArquivo(minuta: Minuta, cnjNumber: string): string {
  return `Rascunho - ${minuta.titulo} - ${cnjNumber}.txt`
}

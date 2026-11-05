/**
 * DADOS DE DEMONSTRAÇÃO do painel. Só o que não tem backend: financeiro, aprovações, carga da
 * equipe, atendimentos e checklist de protocolo. Tudo o que tem endpoint (prazos da agenda,
 * publicações a revisar, alertas) vem da API — ver DashboardView. Cada seção que usa isto
 * mostra o aviso de demonstração; nada aqui deve ser lido como número real do escritório.
 */

export const brl = (valor: number) =>
  new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
    maximumFractionDigits: 0,
  }).format(valor)

export type AprovacaoTipo = 'Peça' | 'Honorário' | 'Despesa' | 'Acesso'

export interface Aprovacao {
  id: string
  tipo: AprovacaoTipo
  titulo: string
  solicitante: string
}

export const aprovacoesIniciais: Aprovacao[] = [
  {
    id: 'ap1',
    tipo: 'Peça',
    titulo: 'Contestação · Souza × Banco Alfa',
    solicitante: 'Marina Lopes',
  },
  {
    id: 'ap2',
    tipo: 'Honorário',
    titulo: 'Proposta de êxito · Reis Transportes',
    solicitante: 'Bruno Lima',
  },
  { id: 'ap3', tipo: 'Despesa', titulo: 'Perícia contábil · R$ 4.200', solicitante: 'Carla Dias' },
  {
    id: 'ap4',
    tipo: 'Acesso',
    titulo: 'Acesso ao sistema do TJ · estagiário',
    solicitante: 'Marina Lopes',
  },
]

export const cargaEquipe = [
  { nome: 'Marina Lopes', tarefas: 12 },
  { nome: 'Bruno Lima', tarefas: 9 },
  { nome: 'Carla Dias', tarefas: 6 },
  { nome: 'Rafael Nunes', tarefas: 3 },
] as const

/** Teto da barra de carga: 14 tarefas = barra cheia. */
export const CARGA_MAXIMA = 14

export const financeiroSocio = { receitaMes: 128_400, emAtraso: 18_700 } as const

export const secretariaKpis = {
  publicacoesSemProcesso: 4,
  atendimentosAguardando: 3,
  retornosLigacao: 2,
} as const

export const aguardandoHumano = [
  {
    id: 'at1',
    cliente: 'Helena Prado',
    motivo: 'Pediu para falar sobre o acordo',
    espera: 'há 12 min',
  },
  {
    id: 'at2',
    cliente: 'Transportes Reis',
    motivo: 'Dúvida sobre prazo de recurso',
    espera: 'há 40 min',
  },
  { id: 'at3', cliente: 'Otávio Mendes', motivo: 'Enviou documentos novos', espera: 'há 1 h' },
] as const

export interface ChecklistItem {
  id: string
  texto: string
  feito: boolean
}

export const checklistProtocoloInicial: readonly ChecklistItem[] = [
  { id: 'cp1', texto: 'Protocolar contestação · 0000777-07.2018', feito: false },
  { id: 'cp2', texto: 'Juntar procuração · Helena Prado', feito: false },
  { id: 'cp3', texto: 'Recolher custas · Reis Transportes', feito: true },
  { id: 'cp4', texto: 'Protocolar manifestação · 0001234-55.2025', feito: false },
] as const

export const financeiroKpis = {
  receberHoje: 22_300,
  emAtraso: 18_700,
  recebidoMes: 128_400,
  pagar7Dias: 9_850,
} as const

/** Entradas e saídas de jun a nov (em milhares de reais). */
export const fluxoCaixa = [
  { mes: 'Jun', entradas: 98, saidas: 71 },
  { mes: 'Jul', entradas: 112, saidas: 80 },
  { mes: 'Ago', entradas: 104, saidas: 76 },
  { mes: 'Set', entradas: 131, saidas: 88 },
  { mes: 'Out', entradas: 145, saidas: 92 },
  { mes: 'Nov', entradas: 128, saidas: 70 },
] as const

/** Escala do gráfico: 170 mil = barra de 140px. */
export const FLUXO_ESCALA_MAX = 170
export const FLUXO_ALTURA_PX = 140

export const conciliacaoInicial = [
  { id: 'c1', descricao: 'Honorários · Reis Transportes', valor: 4_500, data: '04/nov' },
  { id: 'c2', descricao: 'Honorários · Helena Prado', valor: 2_800, data: '03/nov' },
  { id: 'c3', descricao: 'Custas reembolsadas · Souza', valor: 640, data: '03/nov' },
] as const

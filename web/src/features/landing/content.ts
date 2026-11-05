/**
 * Conteúdo da landing. Regras (prompt de produto): nada de depoimento, logotipo de cliente,
 * número de usuários, selo ou métrica inventados; só se afirma o que o produto faz hoje.
 * Marcadores {{PREENCHER}} / {{CONFIRMAR}} dependem de decisão do dono do produto — ver
 * docs/frontend/landing-onboarding.md para a lista consolidada.
 */
import {
  Banknote,
  BriefcaseBusiness,
  CalendarDays,
  Headset,
  KanbanSquare,
  Newspaper,
  PenLine,
  type LucideIcon,
} from 'lucide-react'
import { routes } from '@/config/routes'

export const navLinks = [
  { href: '#como-funciona', label: 'Como funciona' },
  { href: '#recursos', label: 'Recursos' },
  { href: '#seguranca', label: 'Segurança' },
  { href: '#precos', label: 'Custos' },
  { href: '#perguntas', label: 'Perguntas' },
] as const

export const steps = [
  {
    n: '01',
    title: 'A Athena lê o DJEN todo dia',
    text: 'Ela consulta as publicações dos nomes e OABs que você cadastrar e separa o que exige atenção.',
  },
  {
    n: '02',
    title: 'Sugere o tipo e o prazo',
    text: 'Para cada publicação, a Athena propõe o tipo de ato e o prazo, com o fundamento legal à vista.',
  },
  {
    n: '03',
    title: 'Você decide',
    text: 'Aceite, corrija ou rejeite. Só depois da sua decisão o prazo vai para a agenda.',
  },
] as const

export interface Feature {
  icon: LucideIcon
  title: string
  text: string
  /** Estado real no produto: ausente = disponível. */
  status?: 'Em breve' | 'Dados de exemplo'
}

export const features: Feature[] = [
  {
    icon: Newspaper,
    title: 'Publicações e prazos',
    text: 'Fila de publicações do DJEN/CNJ com revisão obrigatória e histórico de cada decisão.',
  },
  {
    icon: BriefcaseBusiness,
    title: 'Processos e clientes',
    text: 'Cadastro com número CNJ, importação por planilha e vínculo automático das publicações.',
  },
  {
    icon: CalendarDays,
    title: 'Agenda',
    text: 'Prazos aceitos e compromissos do escritório em um só calendário.',
    status: 'Em breve',
  },
  {
    icon: Headset,
    title: 'Atendimentos no WhatsApp',
    text: 'A IA responde o básico e transfere para uma pessoa quando o assunto pede.',
    status: 'Em breve',
  },
  {
    icon: PenLine,
    title: 'Criação de peças',
    text: 'Rascunhos para você revisar e assinar — nunca peças prontas sem a sua leitura.',
    status: 'Em breve',
  },
  {
    icon: KanbanSquare,
    title: 'Tarefas em Kanban',
    text: 'Distribua o trabalho da equipe e acompanhe o andamento.',
    status: 'Em breve',
  },
  {
    icon: Banknote,
    title: 'Financeiro e honorários',
    text: 'Honorários e conciliação por escritório. Hoje a tela usa dados de exemplo.',
    status: 'Dados de exemplo',
  },
]

export const roles = [
  {
    id: 'socio',
    label: 'Sócio',
    headline: 'Visão do escritório e controle da equipe',
    items: [
      'Configura o escritório, o que a Athena monitora e como ela trabalha.',
      'Convida a equipe e define o perfil de cada pessoa.',
      'Acompanha a trilha de auditoria das decisões.',
      'Também revisa e decide, como qualquer advogado.',
    ],
  },
  {
    id: 'advogado',
    label: 'Advogado',
    headline: 'Revisa e decide',
    items: [
      'Recebe as sugestões da Athena na fila de revisão.',
      'Aceita, corrige ou rejeita — e registra o porquê.',
      'Acompanha os processos e clientes sob sua responsabilidade.',
    ],
  },
  {
    id: 'secretaria',
    label: 'Secretária',
    headline: 'Organiza para o advogado decidir',
    items: [
      'Vincula publicações sem processo ao processo correto.',
      'Cadastra clientes e processos.',
      'Prepara o que o advogado precisa revisar. {{CONFIRMAR}} permissões exatas do perfil.',
    ],
  },
  {
    id: 'financeiro',
    label: 'Financeiro',
    headline: 'Honorários sem planilha paralela',
    items: [
      'Acompanha honorários e recebimentos do escritório.',
      'Não precisa ver o conteúdo das publicações para trabalhar.',
      'Hoje a área financeira usa dados de exemplo.',
    ],
  },
] as const

export const securityPoints = [
  {
    title: 'Dados isolados por escritório',
    text: 'Cada escritório só enxerga os próprios dados — o isolamento é imposto no banco de dados, não só na tela.',
  },
  {
    title: 'Criptografia',
    text: 'Dados em trânsito protegidos por TLS. Documento, e-mail e telefone de clientes e a OAB dos usuários são criptografados em repouso.',
  },
  {
    title: 'Trilha de auditoria imutável',
    text: 'Quem fez o quê, quando e de onde fica registrado e não pode ser alterado nem apagado antes de 5 anos.',
  },
  {
    title: 'Acesso por perfil',
    text: 'Sócio, advogado, secretária e financeiro veem apenas o que o trabalho de cada um exige.',
  },
  {
    title: 'Autenticação em dois fatores',
    text: 'Proteja a conta com um aplicativo autenticador. Recomendado a todos os sócios.',
  },
  {
    title: 'Revisão humana obrigatória',
    text: 'A IA nunca salva um prazo sozinha. O uso de IA é informado com transparência, como pede a OAB.',
  },
] as const

export const faqs = [
  {
    q: 'A IA substitui o advogado?',
    a: 'Não. A Athena sugere; você decide. Nada vira prazo na agenda sem a sua aceitação, e a responsabilidade final é sempre do advogado.',
  },
  {
    q: 'Quais tribunais são monitorados?',
    a: 'O monitoramento parte do DJEN/CNJ, pelos nomes e OABs que você cadastrar. {{CONFIRMAR}} lista de tribunais cobertos no lançamento.',
  },
  {
    q: 'Como ficam meus dados?',
    a: 'Ficam isolados por escritório, com criptografia e trilha de auditoria. Os dados processuais não são usados para treinar modelos de IA de terceiros. Detalhes na Política de Privacidade.',
  },
  {
    q: 'Posso importar meus processos?',
    a: 'Sim, por planilha CSV (há um modelo para baixar) ou cadastrando um a um. Você também pode começar sem processos e deixar a Athena trazê-los pelo monitoramento.',
  },
  {
    q: 'Funciona no celular?',
    a: 'O site se adapta ao celular. {{CONFIRMAR}} previsão do aplicativo iOS nativo.',
  },
] as const

export const cta = { primary: routes.signup, login: routes.login } as const

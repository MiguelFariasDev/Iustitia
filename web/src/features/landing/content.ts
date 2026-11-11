import {
  Banknote,
  Bell,
  BriefcaseBusiness,
  CalendarDays,
  FileText,
  Gavel,
  LayoutDashboard,
  MessageCircle,
  PenLine,
  Users,
  type LucideIcon,
} from 'lucide-react'

/**
 * Conteúdo da landing page. Fica em dados (e não espalhado em JSX) para ser revisado por quem
 * não programa e testado. REGRA: só descreve o que o produto faz; nada de números, depoimentos,
 * prêmios ou garantias de resultado (Provimento 205/2021 da OAB sobre publicidade).
 */
export type FeatureStatus = 'disponivel' | 'beta' | 'em-evolucao'

export interface Feature {
  id: string
  icon: LucideIcon
  title: string
  description: string
  status: FeatureStatus
}

export const statusLabel: Record<FeatureStatus, string | null> = {
  disponivel: null,
  beta: 'Beta',
  'em-evolucao': 'Em evolução',
}

export const features: Feature[] = [
  {
    id: 'publicacoes',
    icon: Gavel,
    title: 'Publicações e prazos',
    description:
      'Receba as publicações do seu escritório, veja o que exige providência e acompanhe cada prazo até o cumprimento.',
    status: 'disponivel',
  },
  {
    id: 'alertas',
    icon: Bell,
    title: 'Alertas que chamam a atenção certa',
    description:
      'Prazos próximos e itens sem tratamento aparecem em destaque, para nada vencer por esquecimento.',
    status: 'disponivel',
  },
  {
    id: 'agenda',
    icon: CalendarDays,
    title: 'Agenda do escritório',
    description: 'Audiências, prazos e compromissos da equipe num só calendário.',
    status: 'disponivel',
  },
  {
    id: 'processos',
    icon: BriefcaseBusiness,
    title: 'Processos organizados',
    description:
      'Cadastre ou importe processos e mantenha partes, andamentos e responsáveis num único lugar.',
    status: 'disponivel',
  },
  {
    id: 'clientes',
    icon: Users,
    title: 'Clientes e contatos',
    description: 'Histórico de cada cliente e de cada contato, ligado aos processos relacionados.',
    status: 'disponivel',
  },
  {
    id: 'atendimentos',
    icon: MessageCircle,
    title: 'Atendimento por WhatsApp',
    description:
      'Converse com clientes pelo WhatsApp e acompanhe as conversas numa caixa de entrada compartilhada.',
    status: 'disponivel',
  },
  {
    id: 'pecas',
    icon: PenLine,
    title: 'Criação de peças com IA',
    description:
      'Gere uma minuta a partir dos fatos do caso e das fontes do acervo, com citações rastreáveis e revisão humana obrigatória.',
    status: 'beta',
  },
  {
    id: 'documentos',
    icon: FileText,
    title: 'Documentos',
    description: 'Guarde os documentos do escritório e use-os como base para as minutas.',
    status: 'em-evolucao',
  },
  {
    id: 'indicadores',
    icon: LayoutDashboard,
    title: 'Indicadores',
    description: 'Acompanhe a carga de trabalho e o cumprimento de prazos da equipe.',
    status: 'em-evolucao',
  },
  {
    id: 'financeiro',
    icon: Banknote,
    title: 'Financeiro',
    description: 'Honorários e recebimentos do escritório.',
    status: 'em-evolucao',
  },
]

export const steps = [
  {
    title: 'A publicação chega',
    description: 'O sistema reúne as publicações do seu escritório, sem você precisar procurá-las.',
  },
  {
    title: 'A IA sugere, você decide',
    description:
      'A classificação e o prazo vêm como sugestão. Nada é dado como certo sem a sua confirmação.',
  },
  {
    title: 'O prazo vai para a agenda',
    description: 'Confirmado, o prazo entra na agenda da equipe e passa a ser acompanhado por alertas.',
  },
] as const

export const responsibleAi = [
  {
    title: 'Citação com fonte',
    description:
      'Cada afirmação jurídica da minuta aponta o dispositivo ou documento de onde veio, e a fonte oficial fica a um clique.',
  },
  {
    title: 'Sem jurisprudência inventada',
    description:
      'A IA é instruída a usar apenas o que foi recuperado do acervo e o que você informou. Citações que o sistema não consegue conferir são sinalizadas para você verificar.',
  },
  {
    title: 'Revisão humana obrigatória',
    description:
      'Toda minuta sai marcada como rascunho. Enquanto houver citação pendente de verificação, a exportação final fica bloqueada.',
  },
  {
    title: 'Vigência à vista',
    description:
      'Normas revogadas são identificadas, e o sistema avisa quando um texto citado não está vigente.',
  },
] as const

export const security = [
  {
    title: 'Dados isolados por escritório',
    description: 'Cada escritório enxerga somente os seus dados, garantido também no banco de dados.',
  },
  {
    title: 'Perfis de acesso',
    description: 'Sócios, advogados e equipe de apoio com permissões diferentes.',
  },
  {
    title: 'Trilha de auditoria',
    description: 'Registro de quem fez o quê, para você prestar contas quando precisar.',
  },
  {
    title: 'Pensado para a LGPD',
    description: 'Dados pessoais sensíveis protegidos e tratados com finalidade definida.',
  },
] as const

export const faq = [
  {
    q: 'A inteligência artificial decide por mim?',
    a: 'Não. Ela sugere classificações, prazos e minutas, sempre como rascunho. A decisão e a responsabilidade são do advogado, e o sistema foi desenhado para exigir a sua revisão.',
  },
  {
    q: 'Os dados do meu escritório ficam separados dos de outros?',
    a: 'Sim. Cada escritório só acessa os próprios dados, e essa separação é aplicada no próprio banco de dados, não apenas na tela.',
  },
  {
    q: 'O texto das leis exibido é oficial?',
    a: 'Não. O acervo normativo tem caráter informativo e não substitui a publicação oficial. Por isso cada citação traz o link da fonte oficial para conferência.',
  },
  {
    q: 'Atende advogado autônomo ou só escritório?',
    a: 'Os dois. Há cadastro para escritórios com equipe e para advogados que atuam sozinhos.',
  },
  {
    q: 'Preciso instalar alguma coisa?',
    a: 'Não. O acesso é pelo navegador, no computador ou no celular.',
  },
] as const

/** Listas fixas do onboarding. {{CONFIRMAR}}: tribunais efetivamente cobertos pela captura. */
export const tribunais = [
  'STF', 'STJ', 'TST',
  'TRF-1', 'TRF-2', 'TRF-3', 'TRF-4', 'TRF-5', 'TRF-6',
  'TRT-1', 'TRT-2', 'TRT-3', 'TRT-4', 'TRT-5', 'TRT-9', 'TRT-15',
  'TJSP', 'TJRJ', 'TJMG', 'TJRS', 'TJPR', 'TJSC', 'TJBA', 'TJDFT', 'TJGO', 'TJPE', 'TJCE', 'TJES',
] as const

export const timezones = [
  { value: 'America/Sao_Paulo', label: 'Brasília (UTC−3)' },
  { value: 'America/Manaus', label: 'Manaus (UTC−4)' },
  { value: 'America/Cuiaba', label: 'Cuiabá (UTC−4)' },
  { value: 'America/Rio_Branco', label: 'Rio Branco (UTC−5)' },
  { value: 'America/Noronha', label: 'Fernando de Noronha (UTC−2)' },
] as const

export const checkHours = Array.from({ length: 24 }, (_, hour) => `${String(hour).padStart(2, '0')}:00`)

/** Perfis oferecidos no convite (Estagiário fica de fora do onboarding por enquanto). */
export const inviteRoles = [
  { value: 'Partner', label: 'Sócio', summary: 'Configura o escritório, convida a equipe, revisa e decide.' },
  { value: 'Lawyer', label: 'Advogado', summary: 'Revisa as sugestões da Athena e acompanha processos e clientes.' },
  { value: 'Admin', label: 'Secretária', summary: 'Organiza publicações, clientes e processos para o advogado decidir. {{CONFIRMAR}} permissões exatas.' },
  { value: 'Financial', label: 'Financeiro', summary: 'Acompanha honorários e recebimentos (hoje com dados de exemplo).' },
] as const

export type InviteRoleValue = (typeof inviteRoles)[number]['value']

/** Cartões do tour curto do convidado — só o que o perfil enxerga (ver content da landing). */
export const tourByRole: Record<string, { title: string; text: string }[]> = {
  Lawyer: [
    { title: 'Revisão da IA', text: 'Cada publicação chega com a sugestão da Athena. Você aceita, corrige ou rejeita.' },
    { title: 'Processos e clientes', text: 'Acompanhe os processos sob sua responsabilidade e o histórico das decisões.' },
    { title: 'Agenda', text: 'Os prazos aceitos aparecem na agenda. Em breve.' },
  ],
  Admin: [
    { title: 'Publicações sem processo', text: 'Vincule ao processo correto o que a Athena não conseguiu ligar sozinha.' },
    { title: 'Clientes e processos', text: 'Cadastre e mantenha os dados que o advogado usa para decidir.' },
    { title: 'Atendimentos', text: 'Triagem de atendimentos e checklist de protocolo. Em breve.' },
  ],
  Financial: [
    { title: 'Honorários', text: 'Acompanhe honorários e recebimentos do escritório. Hoje com dados de exemplo.' },
    { title: 'Conciliação', text: 'Confira lançamentos sem planilha paralela. Em breve.' },
  ],
  Intern: [
    { title: 'Publicações', text: 'Acompanhe as publicações do escritório e apoie a equipe na organização.' },
    { title: 'Processos', text: 'Consulte processos e clientes que a equipe compartilha com você.' },
  ],
}

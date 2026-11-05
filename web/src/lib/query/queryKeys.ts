/**
 * Fábrica de query keys (hierárquicas) — permite invalidar por prefixo, ex.:
 * queryClient.invalidateQueries({ queryKey: queryKeys.users.all }).
 */
export const queryKeys = {
  me: ['me'] as const,
  dashboard: {
    all: ['dashboard'] as const,
    summary: () => [...queryKeys.dashboard.all, 'summary'] as const,
  },
  search: {
    all: ['search'] as const,
    global: (term: string) => [...queryKeys.search.all, term] as const,
  },
  navCounts: ['nav-counts'] as const,
  clientes: {
    all: ['clientes'] as const,
    // Os filtros entram na chave: cada combinação é uma entrada de cache própria.
    list: (filters: unknown) => [...queryKeys.clientes.all, 'list', filters] as const,
    byId: (id: string) => [...queryKeys.clientes.all, 'detail', id] as const,
  },
  processos: {
    all: ['processos'] as const,
    list: (filters: unknown) => [...queryKeys.processos.all, 'list', filters] as const,
    byId: (id: string) => [...queryKeys.processos.all, 'detail', id] as const,
  },
  publications: {
    all: ['publications'] as const,
    // Os filtros entram na chave: cada combinação é uma entrada de cache própria.
    list: (filters: unknown) => [...queryKeys.publications.all, 'list', filters] as const,
    summary: () => [...queryKeys.publications.all, 'summary'] as const,
    byId: (id: string) => [...queryKeys.publications.all, 'detail', id] as const,
  },
  atendimentos: {
    all: ['atendimentos'] as const,
    list: (filters: unknown) => [...queryKeys.atendimentos.all, 'list', filters] as const,
    byId: (id: string) => [...queryKeys.atendimentos.all, 'detail', id] as const,
    mensagens: (id: string) => [...queryKeys.atendimentos.all, 'mensagens', id] as const,
  },
  alerts: {
    all: ['alerts'] as const,
    list: (filters: unknown) => [...queryKeys.alerts.all, 'list', filters] as const,
    summary: () => [...queryKeys.alerts.all, 'summary'] as const,
    byId: (id: string) => [...queryKeys.alerts.all, 'detail', id] as const,
  },
  agenda: {
    all: ['agenda'] as const,
    /** `weekStart` em ISO (segunda-feira) mantém uma entrada de cache por semana. */
    week: (weekStart: string) => [...queryKeys.agenda.all, 'week', weekStart] as const,
  },
  contatos: {
    all: ['contatos'] as const,
    list: (filters: unknown) => [...queryKeys.contatos.all, 'list', filters] as const,
    byId: (id: string) => [...queryKeys.contatos.all, 'detail', id] as const,
  },
  documentos: {
    all: ['documentos'] as const,
    list: (filters: unknown) => [...queryKeys.documentos.all, 'list', filters] as const,
  },
  indicadores: {
    all: ['indicadores'] as const,
    periodo: (periodo: string) => [...queryKeys.indicadores.all, periodo] as const,
  },
  financeiro: {
    all: ['financeiro'] as const,
    resumo: () => [...queryKeys.financeiro.all, 'resumo'] as const,
    lancamentos: () => [...queryKeys.financeiro.all, 'lancamentos'] as const,
    fluxoCaixa: () => [...queryKeys.financeiro.all, 'fluxo-caixa'] as const,
  },
  tenant: {
    all: ['tenant'] as const,
    current: () => [...queryKeys.tenant.all, 'current'] as const,
    profile: () => [...queryKeys.tenant.all, 'profile'] as const,
    featureFlags: () => [...queryKeys.tenant.all, 'feature-flags'] as const,
  },
  users: {
    all: ['users'] as const,
    list: () => [...queryKeys.users.all, 'list'] as const,
  },
  invites: {
    byToken: (token: string) => ['invites', token] as const,
  },
  profile: ['profile'] as const,
  notificationPreferences: ['notification-preferences'] as const,
  integrations: ['integrations'] as const,
  audit: {
    all: ['audit'] as const,
    list: () => [...queryKeys.audit.all, 'list'] as const,
  },
} as const

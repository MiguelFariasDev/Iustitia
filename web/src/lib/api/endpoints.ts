/** Caminhos relativos a NEXT_PUBLIC_API_URL (…/api/v1). Espelham Platform.Api/Endpoints. */
export const endpoints = {
  status: {
    me: '/me',
    version: '/version',
  },
  auth: {
    login: '/auth/login',
    refresh: '/auth/refresh',
    logout: '/auth/logout',
    /** POST — registra o login de quem entrou direto no Supabase (último acesso + auditoria). */
    session: '/auth/session',
  },
  tenants: {
    create: '/tenants',
    byId: (id: string) => `/tenants/${id}`,
  },
  settings: {
    root: '/settings',
  },
  featureFlags: {
    list: '/feature-flags',
    byKey: (key: string) => `/feature-flags/${encodeURIComponent(key)}`,
  },
  users: {
    list: '/users',
    byId: (id: string) => `/users/${id}`,
    role: (id: string) => `/users/${id}/role`,
    invite: '/users/invite',
    acceptInvite: '/users/accept-invite',
    /** Pendente no backend: consulta pública de convite por token. */
    inviteByToken: (token: string) => `/users/invites/${encodeURIComponent(token)}`,
  },
  audit: {
    list: '/audit',
    byId: (id: string) => `/audit/${id}`,
  },
  /*
   * NÃO existe /dashboard/summary nem /me/pending-counts no backend, e não devem existir:
   * o painel cruza dados de módulos diferentes (publicações do Legal, processos e prazos das
   * Fases 2/3), e um endpoint único obrigaria o Platform a depender dos módulos de negócio —
   * exatamente o que ModuleBoundaryTests proíbe (ver ADR-001).
   *
   * O painel é composto NO CLIENTE, a partir dos endpoints de cada módulo. Ver
   * features/dashboard/api.ts.
   */
  contatos: {
    list: '/contatos',
    byId: (id: string) => `/contatos/${id}`,
    reactivate: (id: string) => `/contatos/${id}/reactivate`,
  },
  tarefas: {
    root: '/tarefas',
    byId: (id: string) => `/tarefas/${id}`,
    status: (id: string) => `/tarefas/${id}/status`,
  },
  tags: {
    root: '/tags',
    byId: (id: string) => `/tags/${id}`,
    atribuicoes: '/tags/atribuicoes',
  },
  agenda: {
    eventos: '/agenda/eventos',
    eventoById: (id: string) => `/agenda/eventos/${id}`,
    reschedule: (id: string) => `/agenda/eventos/${id}/reschedule`,
    /** PATCH { done } — conclui ou reabre o compromisso. */
    baixa: (id: string) => `/agenda/eventos/${id}/baixa`,
  },
  /** Criação de peças com IA (Drafting). A geração é assíncrona: enfileira e se acompanha por status. */
  pecas: {
    gerar: '/pecas/gerar',
    status: (id: string) => `/pecas/${id}/status`,
    byId: (id: string) => `/pecas/${id}`,
    cancelar: (id: string) => `/pecas/${id}/cancelar`,
    citacao: (id: string, citacaoId: string) => `/pecas/${id}/citacoes/${citacaoId}`,
    revisao: (id: string) => `/pecas/${id}/revisao`,
    exportar: (id: string) => `/pecas/${id}/exportar`,
  },
  /** Casos: a unidade de isolamento da geração. Os anexos de um caso nunca entram em outro. */
  casos: {
    list: '/casos',
    documentos: (id: string) => `/casos/${id}/documentos`,
    documento: (id: string, documentoId: string) => `/casos/${id}/documentos/${documentoId}`,
    documentoMarkdown: (id: string, documentoId: string) =>
      `/casos/${id}/documentos/${documentoId}/markdown`,
    revisarDocumento: (id: string, documentoId: string) =>
      `/casos/${id}/documentos/${documentoId}/revisar`,
  },
  clientes: {
    list: '/clientes',
    byId: (id: string) => `/clientes/${id}`,
    reactivate: (id: string) => `/clientes/${id}/reactivate`,
    /** LGPD: GET devolve um JSON com todos os dados do titular (acesso e portabilidade). */
    exportacao: (id: string) => `/clientes/${id}/exportacao`,
    /** LGPD: POST elimina de forma irreversível os dados pessoais do cliente. */
    anonimizar: (id: string) => `/clientes/${id}/anonimizar`,
  },
  financeiro: {
    lancamentos: '/financeiro/lancamentos',
    pagar: (id: string) => `/financeiro/lancamentos/${id}/pagar`,
    excluir: (id: string) => `/financeiro/lancamentos/${id}`,
    resumo: '/financeiro/resumo',
    fluxoCaixa: '/financeiro/fluxo-caixa',
  },
  processos: {
    list: '/processos',
    byId: (id: string) => `/processos/${id}`,
    status: (id: string) => `/processos/${id}/status`,
    archive: (id: string) => `/processos/${id}/archive`,
    /** PATCH { confidencial } — sigilo interno; só Partner ou acima. */
    confidencial: (id: string) => `/processos/${id}/confidencial`,
    /**
     * Consulta de metadados no tribunal (DataJud, via backend) para auto-preencher o
     * cadastro. Limitada a 10 consultas/min por usuário — ver ADR-064.
     */
    consultarTribunal: '/processos/consultar-tribunal',
    /** Pendente no backend: importação em lote (ImportProcessos). */
    import: '/processos/import',
  },
  publications: {
    list: '/publications',
    byId: (id: string) => `/publications/${id}`,
    summary: '/publications/summary',
    /** PATCH — controle de leitura; NÃO é aprovação. */
    read: (id: string) => `/publications/${id}/read`,
    /** PATCH { status, notes } — revisão humana (ADR-012). */
    status: (id: string) => `/publications/${id}/status`,
    /** POST { decision, deadlineDays?, notes? } — decisão completa: status + prazo calculado no servidor + agenda. */
    review: (id: string) => `/publications/${id}/review`,
  },
  /** Inbox do advogado (JWT). O Bot usa /bot/atendimentos com chave de API — nunca daqui. */
  atendimentos: {
    list: '/atendimentos',
    byId: (id: string) => `/atendimentos/${id}`,
    mensagens: (id: string) => `/atendimentos/${id}/mensagens`,
    /** PATCH { status } — Human (assumir), Bot (devolver) ou Closed (encerrar). */
    status: (id: string) => `/atendimentos/${id}/status`,
    /** PUT { clienteId } — vincula a conversa a um cliente do escritório. */
    cliente: (id: string) => `/atendimentos/${id}/cliente`,
  },
  alerts: {
    list: '/alerts',
    byId: (id: string) => `/alerts/${id}`,
    summary: '/alerts/summary',
    /** PATCH — marca todos os alertas do usuário como lidos. */
    readAll: '/alerts/read-all',
    /** PATCH — marca um alerta como lido. */
    read: (id: string) => `/alerts/${id}/read`,
    /** DELETE — descarta (some das listagens; o registro permanece). */
    dismiss: (id: string) => `/alerts/${id}`,
  },
  search: {
    global: '/search',
  },
} as const

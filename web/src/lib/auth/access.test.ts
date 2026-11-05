import {
  canAccess,
  hasTeam,
  screenForPath,
  screenLabel,
  screenRules,
  type AccessContext,
  type Screen,
} from './access'
import { userRoles, type AccountType, type UserRole } from './types'

const ctx = (role: UserRole | null, accountType: AccountType | null = 'Firm'): AccessContext => ({
  role,
  accountType,
})

const telas = Object.keys(screenRules) as Screen[]
const visiveis = (context: AccessContext) => telas.filter((tela) => canAccess(tela, context))

describe('matriz de acesso (espelha as policies da API)', () => {
  it('Owner e Partner de escritório veem tudo', () => {
    expect(visiveis(ctx('Owner'))).toHaveLength(telas.length)
    expect(visiveis(ctx('Partner'))).toHaveLength(telas.length)
  })

  it('Advogado(a) de escritório não vê financeiro, indicadores, usuários, auditoria nem dados do escritório', () => {
    const naoVe = telas.filter((tela) => !canAccess(tela, ctx('Lawyer')))

    expect(naoVe.sort()).toEqual(
      ['finance', 'indicators', 'settings.audit', 'settings.tenant', 'settings.users'].sort()
    )
  })

  it('Estagiário, Administrativo e Financeiro não passam de LawyerOrAbove (hoje levariam 403)', () => {
    for (const papel of ['Intern', 'Admin', 'Financial'] as const) {
      for (const tela of ['processes', 'clients', 'publications', 'alerts', 'contacts'] as const) {
        expect(canAccess(tela, ctx(papel))).toBe(false)
      }
    }
  })

  it('Financeiro enxerga a tela de financeiro e a de todos, e nada do jurídico', () => {
    expect(visiveis(ctx('Financial')).sort()).toEqual(
      [
        'agenda',
        'dashboard',
        'documents',
        'finance',
        'settings.integrations',
        'settings.notifications',
        'settings.profile',
      ].sort()
    )
  })

  it('papel desconhecido só enxerga o que é de todos', () => {
    expect(
      visiveis(ctx(null)).every((tela) => screenRules[tela].roles.length === userRoles.length)
    ).toBe(true)
    expect(canAccess('processes', ctx(null))).toBe(false)
  })
})

describe('conta Solo', () => {
  const solo = ctx('Owner', 'Solo')

  it('não tem a tela de usuários, que só existe com equipe', () => {
    expect(canAccess('settings.users', solo)).toBe(false)
    expect(canAccess('settings.users', ctx('Owner', 'Firm'))).toBe(true)
  })

  it('mantém todo o resto que o titular enxerga', () => {
    expect(telas.filter((tela) => !canAccess(tela, solo))).toEqual(['settings.users'])
  })

  it('a tela do escritório vira "Dados profissionais"', () => {
    expect(screenLabel('settings.tenant', 'Escritório', solo)).toBe('Dados profissionais')
    expect(screenLabel('settings.tenant', 'Escritório', ctx('Owner'))).toBe('Escritório')
    expect(screenLabel('processes', 'Processos', solo)).toBe('Processos')
  })

  it('não tem equipe; conta sem tipo conhecido é tratada como escritório', () => {
    expect(hasTeam(solo)).toBe(false)
    expect(hasTeam(ctx('Owner', null))).toBe(true)
    expect(canAccess('settings.users', ctx('Owner', null))).toBe(true)
  })
})

describe('screenForPath', () => {
  it('resolve a tela pelo prefixo do caminho', () => {
    expect(screenForPath('/processos')).toBe('processes')
    expect(screenForPath('/processos/abc/editar')).toBe('processes')
    expect(screenForPath('/configuracoes/usuarios')).toBe('settings.users')
    expect(screenForPath('/agenda')).toBe('agenda')
  })

  it('rota fora da matriz devolve null', () => {
    expect(screenForPath('/configuracoes')).toBeNull()
    expect(screenForPath('/processosx')).toBeNull()
  })

  it('toda tela da matriz é alcançável por algum caminho', () => {
    const alcancadas = new Set(
      [
        '/painel',
        '/agenda',
        '/publicacoes',
        '/alertas',
        '/processos',
        '/clientes',
        '/contatos',
        '/atendimentos',
        '/financeiro',
        '/criacao-de-pecas',
        '/documentos',
        '/indicadores',
        '/configuracoes/perfil',
        '/configuracoes/escritorio',
        '/configuracoes/usuarios',
        '/configuracoes/notificacoes',
        '/configuracoes/integracoes',
        '/configuracoes/auditoria',
      ].map(screenForPath)
    )

    expect([...alcancadas].sort()).toEqual([...telas].sort())
  })
})

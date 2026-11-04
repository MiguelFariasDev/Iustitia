import {
  toAuditEntry,
  toTeamUser,
  type AuditLogListItemDto,
  type UserListItemDto,
} from './contract'

function userDto(overrides: Partial<UserListItemDto> = {}): UserListItemDto {
  return {
    id: 'user-1',
    name: 'Ana Advogada',
    email: 'ana@escritorio.com',
    specialty: 'Cível',
    role: 'Lawyer',
    isActive: true,
    ...overrides,
  }
}

describe('tradução de usuários da API para a tela de equipe', () => {
  it('mapeia os campos e o papel', () => {
    const user = toTeamUser(userDto())

    expect(user.name).toBe('Ana Advogada')
    expect(user.role).toBe('Lawyer')
    expect(user.status).toBe('active')
  })

  it.each(['lawyer', 'LAWYER', 'Lawyer'])(
    'aceita o papel "%s" independentemente da caixa',
    (role) => {
      expect(toTeamUser(userDto({ role })).role).toBe('Lawyer')
    }
  )

  it('cai no papel de MENOR privilégio quando o valor é desconhecido', () => {
    // Um default permissivo aqui vira base para decisão errada no dia em que alguém ler
    // daqui para habilitar um botão.
    expect(toTeamUser(userDto({ role: 'SuperUsuario' })).role).toBe('Intern')
  })

  it('mapeia inativo para "suspended", não "invited"', () => {
    // 'invited' prometeria uma ação de reenvio de convite que o booleano IsActive não sustenta.
    expect(toTeamUser(userDto({ isActive: false })).status).toBe('suspended')
  })

  it('deixa o último acesso nulo — a listagem da API não devolve esse campo', () => {
    expect(toTeamUser(userDto()).lastSignInAt).toBeNull()
  })
})

describe('tradução do log de auditoria', () => {
  const dto: AuditLogListItemDto = {
    id: 'audit-1',
    userId: '11111111-1111-4111-8111-111111111111',
    action: 'Created',
    entityType: 'User',
    entityId: 'user-9',
    createdAt: '2026-09-28T12:00:00+00:00',
  }

  it('usa createdAt como momento do evento', () => {
    expect(toAuditEntry(dto).occurredAt).toBe('2026-09-28T12:00:00+00:00')
  })

  it('compõe a entidade com tipo e id', () => {
    expect(toAuditEntry(dto).entity).toBe('User (user-9)')
  })

  it('omite o id quando ausente, sem deixar parênteses vazios', () => {
    expect(toAuditEntry({ ...dto, entityId: '' }).entity).toBe('User')
  })

  it('marca o IP como indisponível em vez de inventar — o log não persiste esse dado', () => {
    expect(toAuditEntry(dto).ipAddress).toBe('—')
  })
})

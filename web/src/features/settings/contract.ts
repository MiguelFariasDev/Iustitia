import { userRoles, type UserRole } from '@/lib/auth/types'
import type { AuditEntry, TeamUser, TeamUserStatus } from './types'

/**
 * Camada anticorrupção entre os contratos de Platform.Api e o modelo das telas de
 * configuração — mesma razão de features/publicacoes/contract.ts.
 */

/** GET /me (GetMeResponse). */
export interface MeDto {
  userId: string | null
  email: string | null
  tenantId: string | null
  roles: string[]
}

/** Item de GET /users (UserListItem). */
export interface UserListItemDto {
  id: string
  name: string
  email: string
  specialty: string | null
  role: string
  isActive: boolean
}

export interface ListUsersDto {
  items: UserListItemDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** GET /users/{id} (GetUserByIdResponse). */
export interface UserDetailDto {
  id: string
  tenantId: string
  name: string
  email: string
  specialty: string | null
  role: string
  avatarUrl: string | null
  phone: string | null
  isActive: boolean
  lastLoginAt: string | null
}

/** GET /tenants/{id} (GetTenantByIdResponse). */
export interface TenantDto {
  id: string
  name: string
  cnpj: string
  status: string
  logoUrl: string | null
}

/** Item de GET /audit (AuditLogListItem). */
export interface AuditLogListItemDto {
  id: string
  userId: string
  action: string
  entityType: string
  entityId: string
  createdAt: string
}

export interface ListAuditLogsDto {
  items: AuditLogListItemDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/**
 * O papel vem do backend já em PascalCase (`Owner`, `Lawyer`) e é assim que a UI o tipa —
 * a comparação é feita sem diferenciar caixa mesmo assim, para uma mudança de serialização
 * não quebrar a tela em silêncio.
 *
 * Papel desconhecido vira o de MENOR privilégio, nunca o maior: a tela é só exibição hoje,
 * mas um default permissivo aqui vira base para decisão errada no dia em que alguém ler
 * daqui para habilitar um botão.
 */
function toRole(apiRole: string): UserRole {
  return userRoles.find((role) => role.toLowerCase() === apiRole.toLowerCase()) ?? 'Intern'
}

export function toTeamUser(dto: UserListItemDto): TeamUser {
  // O backend tem um booleano IsActive; a tela distingue três estados. 'suspended' (e não
  // 'invited') é o mapeamento de inativo: um usuário convidado e ainda não aceito também
  // chega como inativo, mas tratá-lo como convite pendente prometeria uma ação de reenvio
  // que o dado não sustenta.
  const status: TeamUserStatus = dto.isActive ? 'active' : 'suspended'

  return {
    id: dto.id,
    name: dto.name,
    email: dto.email,
    role: toRole(dto.role),
    status,
    // O último acesso existe no agregado User (LastLoginAt) e é devolvido por
    // GET /users/{id} — mas NÃO pelo item da listagem (UserListItem). Buscar o detalhe de
    // cada linha só para preencher essa coluna seria N+1 requisições; o campo fica nulo até
    // o contrato da listagem incluí-lo.
    lastSignInAt: null,
  }
}

export function toAuditEntry(dto: AuditLogListItemDto): AuditEntry {
  return {
    id: dto.id,
    occurredAt: dto.createdAt,
    // O log guarda o id do autor; resolver para nome exigiria um join que o endpoint não faz.
    actor: dto.userId,
    action: dto.action,
    entity: `${dto.entityType}${dto.entityId ? ` (${dto.entityId})` : ''}`,
    // IP não é persistido pelo AuditLog atual.
    ipAddress: '—',
  }
}

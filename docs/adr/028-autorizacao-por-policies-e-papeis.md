# ADR-028: Autorização por Policies (RBAC baseado em papéis)

**Status:** Aceito

## Contexto

A partir da Etapa 0.3, o backend passa a validar JWTs do Supabase Auth
(ADR-003) e precisa decidir, por endpoint, quais papéis de usuário
(`UserRole`: `Owner`, `Partner`, `Lawyer`, `Intern`, `Admin`, `Financial`)
podem executar cada ação. Espalhar checagens `if (user.Role != ...)`
manualmente em cada handler é repetitivo e fácil de esquecer num endpoint
novo — o próprio ASP.NET Core já resolve isso via **policy-based
authorization**.

## Decisão

Definir um conjunto fixo de **policies nomeadas** (`Policies.cs`), cada uma
mapeada para um ou mais `UserRole`, e aplicá-las declarativamente em cada
endpoint via `RequireAuthorization(Policies.X)`:

| Policy | Papéis permitidos | Uso típico |
|---|---|---|
| `OwnerOnly` | `Owner` | Gestão do próprio tenant (atualizar dados, suspender/reativar) |
| `PartnerOrAbove` | `Owner`, `Partner` | Convidar/desativar usuários, ver auditoria |
| `LawyerOrAbove` | `Owner`, `Partner`, `Lawyer` | Reservado para features de negócio (Fase 1+) |
| `AdminOnly` | `Admin` | Reservado para operações administrativas internas |
| `FinancialOnly` | `Financial` | Reservado para features financeiras (Fase 1+) |
| `AnyAuthenticatedUser` | Qualquer papel | Ler/editar o próprio perfil, logout |

As policies são resolvidas a partir da claim `user_role` do JWT (ver
ADR-029), configurada como `RoleClaimType` no `JwtBearerOptions` — não da
claim `role` do Supabase, que é reservada.

Não existe (nem é necessária) uma policy "SameTenant": o isolamento por
tenant já é garantido em outra camada — filtro global do EF Core +
Row-Level Security (ADR-004) — então a camada de autorização só decide
"pode fazer essa ação", nunca "em qual tenant", o que evitaria duplicar a
mesma garantia em dois lugares com risco de divergirem.

## Consequências

**Positivas:**
- Regra de autorização visível na assinatura do endpoint
  (`RequireAuthorization(Policies.PartnerOrAbove)`), não escondida dentro
  do handler.
- Novo endpoint só precisa escolher uma policy existente (ou pedir uma
  nova) — não reimplementar a checagem de papel.
- Papéis reservados para funcionalidades futuras (`Admin`, `Financial`,
  `Lawyer`) já têm policy pronta antes mesmo de existir a feature.

**Negativas / trade-offs:**
- Regras de autorização mais finas que "tem este papel" (ex.: "só pode
  editar processos que ele mesmo é responsável") não cabem numa policy
  estática — terão que ser resolvidas dentro do handler quando essas
  features chegarem (Fase 1+), a policy só cobre o filtro grosso de papel.
- Um usuário sem `user_role` na claim (JWT emitido antes do Custom Access
  Token Hook estar ativo, ou usuário sem registro em `public.users` no
  momento do login) falha em **todas** as policies baseadas em papel —
  comportamento fail-closed intencional, não um bug.

## Endpoint público sem policy: cadastro de escritório

`POST /api/v1/tenants` é `AllowAnonymous`, não `OwnerOnly` — é assim que um
escritório novo se cadastra no sistema, antes de existir qualquer usuário
autenticado (ver `CreateTenantHandler` e `docs/compliance/auth-flow.md`).
Aplicar `OwnerOnly` a esse endpoint seria uma contradição lógica: exigiria
já ser Owner de um tenant para poder criar um tenant.

## Vulnerabilidade encontrada e corrigida: IDOR entre tenants nos endpoints de Tenant

`OwnerOnly` valida **que papel** o usuário tem, nunca **em qual tenant**
ele pode agir. Para toda entidade que implementa `IHasTenant` (User,
AuditLog, Settings, FeatureFlag), o filtro global do EF Core (ADR-004)
fecha essa lacuna sozinho. `Tenant`, porém, **é** a própria fronteira de
multi-tenancy — não implementa `IHasTenant` e nunca teria sentido que
implementasse — então nada impedia, antes desta correção, que
`GetTenantByIdHandler`/`UpdateTenantHandler`/`SuspendTenantHandler`/
`ReactivateTenantHandler` operassem sobre **qualquer** tenant do sistema a
partir do `:id` da rota, desde que o chamador fosse Owner de **algum**
escritório (o próprio, ou qualquer outro). Um Owner mal-intencionado
sabendo (ou enumerando) o Id de outro escritório poderia ler seus dados,
editá-los, suspendê-lo ou reativá-lo.

Corrigido adicionando, no início de cada um desses quatro handlers, uma
checagem explícita `currentUser.TenantId != request.TenantId → NotFound`
(não `Forbidden`, para não confirmar a existência do outro tenant a quem
não tem acesso a ele — mesmo padrão de "esconder como se não existisse"
usado pelo filtro global do EF Core para as demais entidades). Validado
com testes HTTP de ponta a ponta (`RlsWithJwtClaimsTests`, Etapa 0.3) que
criam dois tenants distintos e confirmam que o Owner de um não enxerga o
outro por nenhum desses quatro endpoints.

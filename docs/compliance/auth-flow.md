# Fluxo de Autenticação e Autorização

> Ver ADR-003 (Supabase Auth), ADR-004 (Row-Level Security) e ADR-029
> (Custom Access Token Hook) para o racional das decisões descritas aqui.

## Visão geral

O backend .NET **nunca** gerencia senhas nem emite tokens: quem faz isso é
o **Supabase Auth (GoTrue)**, chamado via REST pelo `ISupabaseAuthService`
(`Platform.Infrastructure/Auth/SupabaseAuthService`). O backend só:

1. Repassa credenciais ao Supabase Auth e devolve o `access_token`/
   `refresh_token` recebidos — nunca os armazena.
2. **Valida** o JWT recebido em requisições subsequentes (via JWKS, ver
   `JwtValidationExtensions`).
3. Mantém um registro local de `User` (Platform.Domain) com o **mesmo Id**
   do usuário no Supabase Auth (`auth.users.id`), para guardar dados de
   negócio (papel, especialidade, tenant, etc.) que o Supabase não conhece.

```
Cliente (React/Swift) ──── credenciais ────▶ Backend (.NET) ──REST──▶ Supabase Auth
                                                   │                        │
                                                   │◀──── access/refresh ───┘
                                                   │        token (JWT)
Cliente ◀──── access/refresh token ────────────────┘

Cliente ── Authorization: Bearer <access_token> ──▶ Backend (.NET)
                                                        │
                                          JwtBearer valida assinatura via JWKS
                                          (Authority = {SUPABASE_URL}/auth/v1)
                                                        │
                                          claims (sub, email, tenant_id, user_role)
                                          → ICurrentUser → ITenantContext
```

## Claims do JWT

| Claim | Origem | Uso no backend |
|---|---|---|
| `sub` | Supabase Auth (padrão) | `ICurrentUser.UserId` |
| `email` | Supabase Auth (padrão) | `ICurrentUser.Email` / `NameClaimType` |
| `role` | Supabase Auth (padrão, sempre `"authenticated"`) | **Não usado pelo backend** — reservado ao Postgres (`auth.role()`) |
| `tenant_id` | Custom Access Token Hook | `ICurrentUser.TenantId` → `ITenantContext` (RLS + filtro global do EF Core) |
| `user_role` | Custom Access Token Hook | `ICurrentUser.Roles` / `RoleClaimType` → `[Authorize(Roles=...)]`, policies |

`tenant_id` e `user_role` **não existem** nos JWTs padrão do Supabase — são
injetados pelo Custom Access Token Hook (`infra/supabase/migrations/
004_custom_access_token_hook.sql`), que roda dentro do GoTrue a cada
emissão de token e lê essas duas colunas da tabela `public.users` pelo id
do usuário. Ver seção "Configuração manual" abaixo — a ativação do hook em
si não é uma migration, é um toggle no Dashboard do Supabase.

**Por que `user_role` e não `role`:** o Supabase já reserva a claim `role`
para seu próprio controle interno; sobrescrevê-la quebraria `auth.role()`
no Postgres. Usar um nome próprio evita esse conflito (ver ADR-029).

## Fluxos

### 1. Cadastro de escritório (self-service)

`POST /api/v1/tenants` — **`AllowAnonymous`** (é assim que um escritório
novo se cadastra; não existe usuário autenticado ainda nesse ponto).

`CreateTenantHandler`:
1. Valida CNPJ e e-mail do administrador.
2. Cria o agregado `Tenant` (em memória, ainda não persistido).
3. Chama `ISupabaseAuthService.SignUpAsync` — **antes** de tocar no
   repositório, para não deixar entidades rastreadas pendentes de
   rollback se o SignUp falhar (e-mail já em uso, senha fraca, etc.).
4. Cria o `User` local com `UserId` = id retornado pelo Supabase, papel
   `Owner`, já `Activate()`d (o Owner que acabou de definir a própria
   senha no cadastro entra ativo direto — diferente do convite).
5. Persiste `Tenant` + `User` numa única transação (`IUnitOfWork`).
6. Registra auditoria (`AuditAction.Created`, `tenantId` explícito — não
   há `ICurrentUser.TenantId` ainda nesse ponto, a requisição é anônima).

### 2. Login

`POST /api/v1/auth/login` — `AllowAnonymous`.

`LoginHandler`:
1. `ISupabaseAuthService.SignInAsync(email, password)`.
2. Busca o `User` local pelo mesmo Id da sessão Supabase.
3. Falha (`NotFound`) se autenticou no Supabase mas não existe registro
   local — situação anômala (provisionamento incompleto).
4. Falha (`Forbidden`) se `!user.IsActive` (usuário desativado).
5. `user.RegisterLogin(now)` + `SaveChangesAsync`.
6. Audita `AuditAction.Login` (login é sempre auditado, mesmo com
   sucesso — evento relevante de segurança).
7. Retorna `LoginResponse` com dados do usuário + tokens do Supabase.

### 3. Refresh de token

`POST /api/v1/auth/refresh` — `AllowAnonymous`. Delega diretamente a
`ISupabaseAuthService.RefreshTokenAsync` — sem lógica de negócio adicional
nem auditoria (não é um evento sensível por si só).

### 4. Logout

`POST /api/v1/auth/logout` — requer autenticação
(`Policies.AnyAuthenticatedUser`). Invalida o `access_token` no Supabase
(`SignOutAsync`) e audita `AuditAction.Logout` quando há um `UserId` no
`ICurrentUser` corrente.

### 5. Convite de usuário

`POST /api/v1/users/invite` — requer `Policies.PartnerOrAbove`.

`InviteUserHandler`:
1. Resolve o `tenantId` do `ICurrentUser` (falha `Unauthorized` sem
   tenant — não deveria acontecer atrás de autenticação, é uma defesa
   extra).
2. Valida e-mail e papel solicitado.
3. Verifica duplicidade (mesmo e-mail já cadastrado *neste* tenant).
4. `ISupabaseAuthService.InviteUserAsync` — envia o e-mail de convite via
   Supabase Auth, com `tenant_id`/`name`/`role` como metadata do usuário
   (usados na tela de aceite do convite pelo frontend, e disponíveis via
   `raw_user_meta_data` no Supabase).
5. Cria o `User` local via `User.Invite(...)` (papel, e-mail, tenant —
   **inativo** até o convite ser aceito).

### 6. Aceite de convite

`POST /api/v1/users/accept-invite` — `AllowAnonymous` (o usuário convidado
ainda não tem uma sessão "nossa" — só o token provisório emitido pelo
Supabase quando abre o link do e-mail de convite).

`AcceptInviteHandler`:
1. `ISupabaseAuthService.GetUserFromAccessTokenAsync(accessToken)` —
   identifica o usuário Supabase a partir do token provisório do convite.
2. Busca o `User` local por esse Id; falha se não existir ou se já estiver
   ativo (convite não pode ser aceito duas vezes).
3. `SetPasswordAsync` — define a senha definitiva no Supabase Auth.
4. `user.UpdateProfile(name, ...)` + `user.Activate()`.
5. Audita `AuditAction.Activated`.

## Tenant context na requisição

Depois que o JWT é validado (`app.UseAuthentication()`), o
`TenantContextMiddleware` (`Platform.Api/Middleware`) lê
`ICurrentUser.TenantId` (já populado a partir da claim `tenant_id`) e
chama `ITenantContext.SetTenant(tenantId)`. Isso alimenta dois mecanismos
de isolamento em profundidade:

- **Filtro global do EF Core** (`BaseDbContext`), que restringe toda query
  LINQ ao tenant corrente.
- **RLS no Postgres** (`TenantContextInterceptor`, que executa `SET
  app.current_tenant = '<guid>'` na conexão), que restringe mesmo queries
  que escapassem do filtro do EF Core (ver ADR-004).

Requisições sem `tenant_id` na claim (não deveria acontecer para um
usuário autenticado com registro local válido) simplesmente não definem
`ITenantContext` — o comportamento é fail-closed em ambas as camadas
(zero linhas), nunca "ver tudo".

## Configuração manual necessária (fora deste repositório)

Não existe um projeto Supabase real neste momento (Etapa 0 é validada
localmente via Docker + Testcontainers) — os passos abaixo são para quando
um projeto Supabase for provisionado:

1. **Auth > Providers > Email**: habilitar login por e-mail/senha.
2. **Auth > JWT Keys**: garantir assinatura assimétrica (RS256/ES256) — o
   JwtBearer valida via JWKS, que só existe com chaves assimétricas
   (o esquema legado HS256 com segredo compartilhado não é suportado
   por este design).
3. Aplicar `infra/supabase/migrations/004_custom_access_token_hook.sql`
   (via `npx supabase db push` ou SQL Editor do Dashboard).
4. **Auth > Hooks > Customize Access Token (JWT) Claims**: apontar para a
   function `public.custom_access_token_hook` criada no passo anterior.
   Esse toggle não tem equivalente em migration/CLI — é uma configuração
   do projeto, não do schema do banco.
5. Preencher `Supabase:Url`, `Supabase:AnonKey`, `Supabase:ServiceRoleKey`
   em configuração real (User Secrets/Key Vault) — nunca commitar.

## O que ainda não foi validado end-to-end

Sem um projeto Supabase real, os itens abaixo foram validados **em
partes** (unitariamente, ou contra a REST API/Postgres simulados), mas
não como um fluxo HTTP completo contra o GoTrue de verdade:

- Emissão real de e-mail de convite (`InviteUserAsync`) e do link de
  aceite consumido pelo frontend.
- O toggle do Custom Access Token Hook em si (passo 4 acima) — a function
  SQL foi testada isoladamente contra Postgres local (ver ADR-029), mas o
  GoTrue chamando essa function automaticamente só pode ser confirmado
  com um projeto Supabase real.

# Segurança — Autenticação, Autorização e Isolamento de Dados

> Documento de compliance vivo do repositório: deve ser atualizado
> conforme decisões reais forem tomadas. Ver `docs/compliance/auth-flow.md`
> para o passo a passo dos fluxos, e ADR-003/004/028/029 para o racional
> das decisões.

## Autenticação

- [x] Nenhuma senha é armazenada ou processada pelo backend .NET — delegado
      integralmente ao Supabase Auth (ADR-003).
- [x] JWT validado por assinatura via JWKS (`Authority` = descoberta OIDC
      do Supabase), nunca por segredo compartilhado hardcoded.
- [x] `OnAuthenticationFailed` loga o tipo/mensagem da falha, **nunca** o
      token em si (ver `JwtValidationExtensions`).
- [ ] MFA (TOTP) — habilitado no Supabase Auth, mas ainda não exigido/
      forçado por policy no backend (pendente de decisão de produto sobre
      quais papéis exigem MFA obrigatório).
- [ ] Rate limiting em `/auth/login` e `/auth/refresh` contra força bruta —
      pendente (Etapa 0.6, Observabilidade/Resiliência, ou delegado ao
      rate limiting nativo do Supabase Auth — a confirmar qual camada
      assume isso).

## Autorização

- [x] RBAC por policies nomeadas (`Policies.cs`/`PolicyExtensions`),
      baseado na claim `user_role` — ver ADR-028.
- [x] Toda claim de papel vem de `user_role`, nunca de `role` (reservada
      pelo Supabase) — ver ADR-029.
- [x] Endpoints protegidos declaram a policy explicitamente
      (`RequireAuthorization(Policies.X)`); os únicos `AllowAnonymous` são
      cadastro de escritório, login, refresh e aceite de convite — todos
      justificados em `docs/compliance/auth-flow.md`.
- [ ] Autorização granular por recurso (ex.: "só o advogado responsável
      pode editar este processo") — não se aplica ainda; só existe nas
      features de Legal/Workflow (Fase 1+).

## Isolamento multi-tenant

- [x] Row-Level Security em todas as tabelas com `tenant_id`, validado
      empiricamente contra Postgres real com `app_user` (ver ADR-004).
- [x] Filtro global do EF Core como segunda camada de defesa (mesmo
      isolamento, aplicado na aplicação — não substitui o RLS).
- [x] Comportamento fail-closed: sem `app.current_tenant` definido, zero
      linhas — nunca degrada para "ver tudo" (ADR-004).
- [x] `tenant_id` da claim do JWT (via Custom Access Token Hook) é a
      origem de `ITenantContext` em requisições HTTP — nunca aceito como
      parâmetro de entrada do cliente (ex.: nunca lido de um header
      arbitrário ou do corpo da requisição).

## Segredos

- [x] `Supabase:ServiceRoleKey`/`AnonKey`, connection strings e chaves de
      criptografia de backup nunca commitados — User Secrets em dev, Key
      Vault/GitHub Secrets em CI/produção (ver `docs/compliance/backups.md`).
- [x] Token de acesso/refresh do Supabase nunca persistido pelo backend —
      só repassado ao cliente na resposta de login/refresh.
- [ ] Rotação periódica de `ServiceRoleKey` — processo a definir quando
      houver um projeto Supabase real.

## Auditoria

- [x] `IAuditService` registra ações sensíveis (login, logout, criação de
      tenant, convite, ativação de usuário) com `performedByUserId` e
      `tenantId` explícitos — inclusive em fluxos anônimos (cadastro de
      escritório, aceite de convite), onde `ICurrentUser` ainda não tem
      tenant/usuário resolvido.
- [x] Falha ao gravar auditoria nunca derruba a operação de negócio
      principal (`AuditService` faz seu próprio `SaveChangesAsync`,
      separado da transação do handler que a chamou) — só loga um warning.
- [ ] Retenção/expurgo de audit logs conforme exigência legal (ver
      `docs/compliance/lgpd.md` e `docs/compliance/oab.md`) — resolvido em
      parte pela camada de backup mensal de 5 anos (`docs/compliance/
      backups.md`), mas sem processo de expurgo automatizado ainda.

## Erros e mensagens

- [x] Erros de negócio nunca vazam stack trace/mensagem de exceção interna
      — `Result`/`Error` tipado, convertido em `ProblemDetails` com
      mensagem controlada (`ResultExtensions.ToHttpResult`).
- [x] `ExceptionHandlingMiddleware` só expõe a mensagem real da exceção em
      `IHostEnvironment.IsDevelopment()`; em produção, mensagem genérica.

## Pendências conhecidas desta etapa

- [ ] Testes automatizados de fluxo de auth/JWT/policies contra HTTP real
      (Parte 9 do prompt da Etapa 0.3) — em andamento.
- [ ] Validação end-to-end do Custom Access Token Hook contra um GoTrue
      real (só foi possível validar a função SQL isoladamente, sem projeto
      Supabase provisionado — ver ADR-029).

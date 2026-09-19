# ADR-029: Custom Access Token Hook para claims de tenant e papel

**Status:** Aceito

## Contexto

O backend precisa saber, a partir de cada requisição autenticada, **qual
tenant** e **qual papel** o usuário tem — sem isso, `ITenantContext` (RLS +
filtro global do EF Core, ver ADR-004) e as policies de autorização
(ADR-028) não têm o que ler. O JWT padrão emitido pelo Supabase Auth só
contém claims genéricas (`sub`, `email`, `role` sempre `"authenticated"`)
— nenhuma delas carrega `tenant_id` nem o papel de negócio do usuário.

Duas alternativas foram consideradas:

1. **Consultar o banco a cada requisição** para resolver tenant/papel a
   partir do `sub` do JWT — simples, mas adiciona uma query extra (e uma
   dependência de disponibilidade do banco) só para autorização, em toda
   requisição autenticada.
2. **Injetar essas informações diretamente no JWT**, via **Custom Access
   Token Hook** do Supabase Auth — uma função Postgres que o GoTrue chama
   toda vez que emite um novo access token (login ou refresh).

## Decisão

Usar a opção 2: `public.custom_access_token_hook`
(`infra/supabase/migrations/004_custom_access_token_hook.sql`), que lê
`tenant_id` e `role` de `public.users` (pelo id do usuário, que é o mesmo
id de `auth.users` — ver `User.Create`/`User.Invite`) e os injeta no JWT
como `tenant_id` e **`user_role`**.

**Por que `user_role` e não `role`:** o Supabase já usa a claim `role` para
seu próprio controle interno — sempre `"authenticated"` para um usuário
logado, consumida por `auth.role()` no Postgres (usada, por exemplo, por
policies RLS padrão do Supabase). Reaproveitar esse nome para o papel de
negócio (`Owner`, `Partner`, ...) sobrescreveria essa claim reservada e
quebraria qualquer mecanismo do Supabase que dependa dela. Um nome
próprio (`user_role`) elimina o conflito — e é o valor configurado como
`RoleClaimType` em `JwtValidationExtensions`, para que
`[Authorize(Roles=...)]`/`ClaimsPrincipal.IsInRole()` do ASP.NET Core
enxerguem o papel certo automaticamente.

### Acesso do hook à tabela `users` sob RLS

`public.users` tem RLS habilitado (ADR-004), com uma policy que exige
`app.current_tenant` definido na sessão. O hook roda como a role técnica
`supabase_auth_admin` (usada internamente pelo GoTrue), numa conexão que
nunca passa pelo `TenantContextInterceptor` da aplicação — sem essa
variável de sessão, a policy existente bloquearia a leitura (fail closed),
e o JWT sairia sem `tenant_id`/`user_role`.

A migration resolve isso com grants explícitos e uma **segunda policy
permissiva**, restrita a essa role:

```sql
GRANT SELECT ON public.users TO supabase_auth_admin;

CREATE POLICY allow_auth_admin_read_users ON public.users
  AS PERMISSIVE FOR SELECT
  TO supabase_auth_admin
  USING (true);
```

Como policies permissivas são combinadas com `OR`, isso concede leitura
irrestrita **apenas** para `supabase_auth_admin` — o isolamento por tenant
de `app_user` (a role usada pela aplicação em runtime) não é afetado.

Esse padrão foi validado empiricamente contra Postgres real (não apenas
lido na documentação do Supabase): rodado dentro de uma transação
`BEGIN`/`ROLLBACK` no banco de desenvolvimento local, criando roles
`supabase_auth_admin`/`authenticated`/`anon` temporárias (que só existem
de fato num projeto Supabase real), confirmando três coisas antes do
`ROLLBACK`:

1. A function retorna o `event` com `tenant_id`/`user_role` corretamente
   injetados a partir de uma linha real de `public.users`.
2. `SET ROLE supabase_auth_admin; SELECT ... FROM public.users;` enxerga a
   linha (a policy nova funciona).
3. `SET ROLE app_user; SELECT ... FROM public.users;` continua retornando
   zero linhas sem `app.current_tenant` definido (a policy antiga não foi
   enfraquecida).

## Consequências

**Positivas:**
- Zero round-trips extras ao banco para resolver tenant/papel em
  requisições autenticadas — já vêm no JWT, validado só por assinatura
  (JWKS).
- `ITenantContext`/policies funcionam de forma uniforme entre login
  recém-feito e token renovado via refresh (o hook roda nos dois casos).

**Negativas / trade-offs:**
- Papel/tenant no JWT ficam **desatualizados até o próximo refresh**: se
  um Owner muda o papel de um usuário, o JWT já emitido continua com o
  papel antigo até expirar/renovar. Aceitável para este produto (troca de
  papel não é uma ação de emergência que precise de efeito imediato), mas
  deve ser documentado ao usuário final se aparecer como confusão de
  suporte.
- A ativação do hook (Dashboard do Supabase, Authentication → Hooks) é uma
  configuração manual por projeto, sem equivalente em CLI/migration — ver
  `docs/compliance/auth-flow.md`, seção "Configuração manual".
- Só pôde ser testado deste modo controlado (transação com roles
  simuladas) porque não existe ainda um projeto Supabase real neste
  ambiente — o comportamento do GoTrue chamando o hook automaticamente em
  login/refresh reais fica como validação pendente para quando houver um
  projeto Supabase provisionado.

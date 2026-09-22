-- ============================================================================
-- 004_custom_access_token_hook.sql — Custom Access Token Hook do Supabase Auth
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisito: 002_rls.sql (tabela users com RLS habilitado)
-- Ver ADR-029 (Custom Access Token Hook) e docs/compliance/auth-flow.md.
-- ============================================================================
-- Este hook roda dentro do GoTrue (Supabase Auth) a cada emissão de access token
-- (login, refresh) e injeta duas claims customizadas no JWT, lidas da tabela
-- public.users pelo id do usuário autenticado (que é o MESMO id de auth.users —
-- ver User.Create/User.Invite em Platform.Domain, que recebem o UserId do Supabase
-- em vez de gerar um novo):
--
--   - tenant_id:  usado por TenantContextMiddleware/CurrentUserAccessor (Platform.Api)
--                 para popular ITenantContext em cada requisição (ver ADR-004).
--   - user_role:  usado como RoleClaimType pela autenticação JWT (ver
--                 JwtValidationExtensions) e pelas políticas de autorização
--                 (Policies.cs) — NUNCA usar o nome "role": essa claim já é
--                 reservada pelo próprio Supabase (sempre "authenticated" para
--                 usuários logados, consumida por auth.role() no Postgres) e
--                 seria sobrescrita/conflitaria com ela.
--
-- Depois de aplicar esta migration, a ativação do hook é feita manualmente no
-- Supabase Dashboard (Authentication → Hooks → Customize Access Token (JWT)
-- Claims), apontando para public.custom_access_token_hook — não há API/CLI
-- para isso em projetos self-hosted/dev local, por isso não está automatizado
-- aqui (ver docs/compliance/auth-flow.md, seção "Configuração manual").
-- ============================================================================

CREATE OR REPLACE FUNCTION public.custom_access_token_hook(event jsonb)
RETURNS jsonb
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
  claims jsonb;
  user_tenant_id uuid;
  user_role text;
BEGIN
  SELECT tenant_id, role
    INTO user_tenant_id, user_role
    FROM public.users
    WHERE id = (event->>'user_id')::uuid;

  claims := event->'claims';

  IF user_tenant_id IS NOT NULL THEN
    claims := jsonb_set(claims, '{tenant_id}', to_jsonb(user_tenant_id::text));
  END IF;

  IF user_role IS NOT NULL THEN
    claims := jsonb_set(claims, '{user_role}', to_jsonb(user_role));
  END IF;

  event := jsonb_set(event, '{claims}', claims);

  RETURN event;
END;
$$;

-- GoTrue conecta como supabase_auth_admin, uma role separada de app_user (a role
-- usada pela aplicação em runtime) e de postgres/service_role — precisa de acesso
-- explícito à function e à tabela consultada dentro dela.

GRANT USAGE ON SCHEMA public TO supabase_auth_admin;

GRANT EXECUTE ON FUNCTION public.custom_access_token_hook TO supabase_auth_admin;
REVOKE EXECUTE ON FUNCTION public.custom_access_token_hook FROM authenticated, anon, public;

-- users já tem RLS habilitado (ver 002_rls.sql) com uma policy que exige
-- app.current_tenant definido na sessão — o hook do GoTrue não passa por
-- TenantContextInterceptor e não define essa variável, então sem esta policy
-- adicional supabase_auth_admin leria zero linhas (fail closed) e o JWT sairia
-- sem tenant_id/user_role. Policies permissivas são combinadas com OR, então
-- esta regra concede leitura ampla apenas para essa role técnica, sem afetar o
-- isolamento por tenant já aplicado a app_user.
GRANT SELECT ON public.users TO supabase_auth_admin;

CREATE POLICY allow_auth_admin_read_users ON public.users
  AS PERMISSIVE FOR SELECT
  TO supabase_auth_admin
  USING (true);

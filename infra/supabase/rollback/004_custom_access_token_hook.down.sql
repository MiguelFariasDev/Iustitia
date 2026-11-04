-- Reverte 004_custom_access_token_hook.sql
--
-- ANTES: desative o hook em Supabase Dashboard -> Authentication -> Hooks.
-- Com o hook ativo e a função removida, o Supabase Auth falha ao emitir tokens
-- (login e refresh param de funcionar).
DROP POLICY IF EXISTS allow_auth_admin_read_users ON public.users;
REVOKE SELECT ON public.users FROM supabase_auth_admin;
DROP FUNCTION IF EXISTS public.custom_access_token_hook(jsonb);

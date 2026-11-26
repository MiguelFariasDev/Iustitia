-- Reverte 022_last_login_sync.sql
-- Remove o trigger e a função. O backfill de users.last_login_at NÃO é desfeito: os valores vieram de
-- auth.users.last_sign_in_at e são dados corretos, não estrutura.
DO $$
BEGIN
  IF to_regclass('auth.users') IS NOT NULL THEN
    DROP TRIGGER IF EXISTS on_auth_user_signed_in ON auth.users;
  END IF;
END;
$$;

DROP FUNCTION IF EXISTS public.sync_last_login();

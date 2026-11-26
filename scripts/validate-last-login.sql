-- Verifica a sincronização de users.last_login_at (migration 022).
-- Uso: psql "$SUPABASE_DB_URL" -v ON_ERROR_STOP=1 -f scripts/validate-last-login.sql
-- Falha (exit 3) se algum usuário que já entrou no Supabase continua com last_login_at NULL.
do $$
declare faltando int;
begin
  select count(*) into faltando
    from public.users u
    join auth.users a on a.id = u.id
   where a.last_sign_in_at is not null
     and u.last_login_at is null;

  if faltando > 0 then
    raise exception 'last_login_at NULL para % usuário(s) que já entraram — aplique 022_last_login_sync.sql', faltando;
  end if;

  if not exists (select 1 from pg_trigger where tgname = 'on_auth_user_signed_in' and not tgisinternal) then
    raise exception 'trigger on_auth_user_signed_in ausente em auth.users';
  end if;

  raise notice 'last_login_at: ok';
end $$;

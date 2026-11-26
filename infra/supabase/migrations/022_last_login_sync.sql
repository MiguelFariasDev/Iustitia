-- 022 — último acesso do usuário (users.last_login_at)
--
-- Problema: o web e o mobile entram direto no Supabase Auth e nunca chamam POST /auth/login, o único
-- lugar que gravava users.last_login_at. A coluna ficava NULL para todos. O Supabase já guarda o
-- instante em auth.users.last_sign_in_at; este trigger o copia para public.users a cada login (qualquer
-- caminho: senha, link mágico, reset), e o backfill corrige quem já tinha entrado.
--
-- A auditoria do login (AuditAction.Login) NÃO sai daqui: é gravada por POST /auth/session, chamado pelo
-- cliente logo depois de entrar.
--
-- Idempotente: pode rodar de novo sem efeito colateral.

create or replace function public.sync_last_login()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  update public.users
     set last_login_at = new.last_sign_in_at
   where id = new.id
     and last_login_at is distinct from new.last_sign_in_at;
  return null;
end;
$$;

-- O schema "auth" é do Supabase: num Postgres puro (testes de integração, desenvolvimento sem Supabase) ele não
-- existe e o trigger/backfill são simplesmente pulados.
do $$
begin
  if to_regclass('auth.users') is null then
    raise notice 'auth.users ausente (Postgres sem Supabase): trigger de último acesso não criado.';
    return;
  end if;

  drop trigger if exists on_auth_user_signed_in on auth.users;
  create trigger on_auth_user_signed_in
    after update of last_sign_in_at on auth.users
    for each row
    when (new.last_sign_in_at is not null)
    execute function public.sync_last_login();

  -- Backfill: quem já entrou e ficou com NULL.
  update public.users u
     set last_login_at = a.last_sign_in_at
    from auth.users a
   where a.id = u.id
     and u.last_login_at is null
     and a.last_sign_in_at is not null;
end;
$$;

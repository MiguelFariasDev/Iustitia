-- ============================================================================
-- 015_account_type_claim.sql — claim `account_type` no JWT (Custom Access Token Hook).
--
-- Pré-requisitos:
--   - 004_custom_access_token_hook.sql (a função que este arquivo substitui)
--   - Migration do EF Core do PlatformDbContext que cria tenants.account_type, tenants.cpf e
--     torna tenants.cnpj anulável (gerar com `dotnet ef migrations add AddTenantAccountType`
--     — ver docs/adr/068-tipo-de-conta-solo-e-firm.md).
--
-- A claim serve só para a INTERFACE decidir o que mostrar (ver web/src/lib/auth/access.ts):
-- quem autoriza de verdade são as policies da API e o RLS. Por isso um tenant sem valor (ou
-- um usuário sem tenant) simplesmente não recebe a claim, e o frontend o trata como Firm.
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
  tenant_account_type text;
BEGIN
  SELECT tenant_id, role
    INTO user_tenant_id, user_role
    FROM public.users
    WHERE id = (event->>'user_id')::uuid;

  claims := event->'claims';

  IF user_tenant_id IS NOT NULL THEN
    claims := jsonb_set(claims, '{tenant_id}', to_jsonb(user_tenant_id::text));

    SELECT account_type
      INTO tenant_account_type
      FROM public.tenants
      WHERE id = user_tenant_id;

    IF tenant_account_type IS NOT NULL THEN
      claims := jsonb_set(claims, '{account_type}', to_jsonb(tenant_account_type));
    END IF;
  END IF;

  IF user_role IS NOT NULL THEN
    claims := jsonb_set(claims, '{user_role}', to_jsonb(user_role));
  END IF;

  event := jsonb_set(event, '{claims}', claims);

  RETURN event;
END;
$$;

-- supabase_auth_admin é a role com que o GoTrue executa o hook; tenants tem RLS (002_rls.sql)
-- com policy que exige app.current_tenant, que o hook não define — mesma situação de `users`
-- em 004. A policy permissiva abaixo concede leitura só a essa role técnica.
GRANT SELECT ON public.tenants TO supabase_auth_admin;

CREATE POLICY allow_auth_admin_read_tenants ON public.tenants
  AS PERMISSIVE FOR SELECT
  TO supabase_auth_admin
  USING (true);

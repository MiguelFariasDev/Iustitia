# postgresql — não aplicável

O banco do produto é PostgreSQL gerenciado pelo **Supabase** (ADR-002), fora do
Azure. Não há Azure Database for PostgreSQL a provisionar.

O que existe do lado Azure para o banco:

- **Migrations do EF Core**: Container App Job `caj-advocacia-db-migrate-<env>`
  (módulo `container-apps`), imagem `advocacia-migrator` (`infra/docker/migrator.Dockerfile`).
- **Migrations SQL do Supabase** (RLS, extensões, hook): `.github/workflows/cd-supabase.yml`.
- **Backups `pg_dump`**: containers `db-backups-*` no módulo `blob-storage`,
  alimentados por `.github/workflows/backup.yml`.
- **Connection strings**: segredos `ConnectionStrings--DefaultConnection` (role
  `app_user`, sujeita a RLS) e `ConnectionStrings--Migrations` (dono do schema) no
  Key Vault, gravados por `scripts/keyvault-set-secrets.sh`.

Se um dia o banco for migrado para dentro do Azure (ex.: exigência de soberania de
dados), este é o lugar do módulo `azurerm_postgresql_flexible_server`.

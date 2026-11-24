# Runbook — restauração de backup do banco

> Um backup que nunca foi restaurado não é um backup confiável. Este procedimento deve ser **ensaiado a cada trimestre**
> (ver "Ensaio trimestral"), e não só descoberto no dia do desastre.

## Metas (a confirmar com o negócio)

| Meta | Valor de referência | Observação |
|---|---|---|
| **RPO** (quanto dado se aceita perder) | até 24 h com o `pg_dump` diário; minutos com **PITR** do Supabase | PITR exige plano Team+ — ver `docs/compliance/backups.md` |
| **RTO** (tempo para voltar) | a medir no ensaio | Registre o tempo real a cada ensaio |

## O que existe

1. **Supabase**: backup diário e PITR (painel). É o caminho mais rápido para "voltei no tempo até 10h32".
2. **`pg_dump` cifrado** no Azure Blob (`db-backups-daily|weekly|monthly`), gerado pelo workflow `Backup do Banco`.
   Arquivo: `advocacia_db_<tier>_<AAAAMMDDTHHMMSSZ>.sql.gz.age` — SQL em texto, comprimido, cifrado com **chave pública**.
3. **Storage** (arquivos): workflow `backup-storage.yml`, cópia aditiva (nunca apaga o que sumiu da origem).

## Pré-requisito: a identidade (chave privada) do backup

A chave **privada** `age` não está no CI (de propósito). Quem restaura precisa dela, guardada fora do pipeline (cofre de
senhas da sociedade + cópia offline). **Sem ela os dumps não abrem.** Confirme no ensaio que as duas cópias existem e
funcionam.

## Restaurar um `pg_dump` (passo a passo)

> Restaure **sempre num banco novo**, nunca por cima do de produção. Só depois de validar, decida o que fazer com ele.

```bash
# 1) Baixar o backup mais recente do tier desejado
az storage blob list --container-name db-backups-daily --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
   --query "sort_by([], &properties.lastModified)[-1].name" -o tsv
az storage blob download --container-name db-backups-daily --name <NOME> --file backup.sql.gz.age \
   --connection-string "$AZURE_STORAGE_CONNECTION_STRING"

# 2) Decifrar (identidade privada) e descomprimir
age -d -i backup-identity.txt backup.sql.gz.age | gunzip > backup.sql

# 3) Criar o banco de destino e as roles que o dump referencia
createdb -h <host> -U postgres advocacia_restore
psql -h <host> -U postgres -d advocacia_restore -c "CREATE ROLE app_user NOLOGIN;"   # demais roles do Supabase, se for fora dele

# 4) Restaurar
psql -h <host> -U postgres -d advocacia_restore -v ON_ERROR_STOP=1 -f backup.sql
```

O dump é `--no-owner --no-privileges`: **donos e grants não vêm no arquivo**. Depois de restaurar fora do Supabase, reaplique
os grants e o RLS (`npx supabase db push` com as migrations SQL; as policies vêm no dump, os `GRANT` não).

## Validar a restauração

```sql
SELECT count(*) FROM tenants;  SELECT count(*) FROM clientes;  SELECT count(*) FROM processos;
SELECT count(*) FROM publications;  SELECT count(*) FROM lancamentos;
SELECT max(created_at) FROM publications;      -- confere até que ponto os dados vão (RPO real)
-- Campos cifrados: a aplicação precisa das MESMAS chaves Encryption--* do Key Vault
SELECT documento FROM clientes LIMIT 1;        -- deve parecer cifrado (base64), nunca CPF em claro
```

Suba uma instância de staging da API apontando para o banco restaurado e leia um cliente: o documento só decifra se as
chaves de campo (`Encryption--*`) forem as mesmas do ambiente original — **guarde-as com a mesma disciplina da identidade
do backup** (`docs/runbooks/field-encryption.md`).

## Perdi um dado pontual (não o banco inteiro)

Não restaure produção por cima. Restaure num banco **de apoio**, extraia as linhas (`COPY (SELECT …) TO …`) e reinsira
só o necessário, dentro do escritório certo (RLS!).

## Ensaio trimestral — checklist

- [ ] Baixar e **decifrar** o backup mais recente de cada tier (diário, semanal, mensal).
- [ ] Restaurar num banco novo e rodar as validações acima. **Registrar o tempo total** (RTO real).
- [ ] Subir a API de staging contra ele e abrir um cliente e um processo.
- [ ] Confirmar que a identidade privada está nos **dois** locais e abre o arquivo.
- [ ] Confirmar que o último workflow de backup terminou verde e que a notificação de falha chega (forçar uma falha em staging).
- [ ] Registrar: data, responsável, tempo, problemas e correções (neste arquivo ou no sistema de chamados).

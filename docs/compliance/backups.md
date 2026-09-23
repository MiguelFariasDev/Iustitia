# Política de Backups

> Ver ADR-027 para o racional completo desta estratégia em camadas.

## Camadas de backup

| Camada | Mecanismo | Frequência | Retenção | Workflow/config |
|---|---|---|---|---|
| 1 | Backup automático do Supabase | Diário | 7 dias (Pro) / 14 dias (Team) | Dashboard do Supabase |
| 2 | PITR (Point-in-Time Recovery) | Contínuo | Conforme plano (Team+) | Dashboard do Supabase — add-on |
| 3 | `pg_dump` → Azure Blob | Diário | 30 dias | `.github/workflows/backup.yml` |
| 4 | `pg_dump` → Azure Blob | Semanal | 1 ano | `.github/workflows/backup.yml` |
| 5 | `pg_dump` → Azure Blob | Mensal | 5 anos | `.github/workflows/backup.yml` |
| 6 | Supabase Storage → Azure Blob | Diário | 30 dias hot, depois cool/archive até 1 ano (Lifecycle Management Policy do Storage Account) | `.github/workflows/backup-storage.yml` |

A retenção de 5 anos na camada mensal está alinhada com a exigência legal
de retenção de dados de processos/audit logs (LGPD + OAB).

## Backup de arquivos nunca replica deleções

O workflow de backup do Supabase Storage usa `rclone copy` (cópia
aditiva), nunca `rclone sync` (espelhamento, que apagaria do backup
qualquer arquivo removido da origem no mesmo dia). Um arquivo apagado por
engano ou por um bug precisa continuar recuperável a partir do backup até
a política de retenção expirar — não pode desaparecer só porque
desapareceu da origem.

## Criptografia

Todo `pg_dump` é comprimido (`gzip`) e criptografado (`age` ou `gpg`) antes
do upload para o Azure Blob Storage. A chave de criptografia vive em
`BACKUP_ENCRYPTION_KEY` (GitHub Secrets) — **nunca** em texto claro no
repositório ou em log de CI.

## Segredos necessários (GitHub Actions)

| Secret | Uso |
|---|---|
| `SUPABASE_DB_URL` | Connection string para `pg_dump` |
| `SUPABASE_SERVICE_ROLE_KEY` | Acesso ao Supabase Storage (backup de arquivos, usado como secret key S3) |
| `SUPABASE_S3_ENDPOINT` | Endpoint S3-compatible do Supabase Storage (rclone) |
| `SUPABASE_S3_ACCESS_KEY_ID` | Access key S3-compatible do Supabase Storage (rclone) |
| `BACKUP_ENCRYPTION_KEY` | Chave de criptografia dos dumps (`age`) |
| `AZURE_STORAGE_CONNECTION_STRING` | Upload para Azure Blob Storage |
| `SLACK_WEBHOOK_URL` (ou equivalente) | Notificação em caso de falha |

## Monitoramento e verificação

- **Alertas de falha:** todo workflow de backup notifica (Slack/e-mail/Teams)
  em caso de falha — um backup que falha silenciosamente é pior do que não
  ter backup, porque cria falsa sensação de segurança.
- **Verificação de integridade:** `pg_restore --list <arquivo>` roda como
  parte do workflow, para confirmar que o dump não está corrompido antes
  de considerá-lo "concluído com sucesso".
- **Teste de restauração trimestral:** restaurar um backup completo em
  ambiente de staging, documentar o resultado (tempo de restauração,
  problemas encontrados). Um backup nunca restaurado com sucesso não é um
  backup confiável — é só um arquivo.

## LGPD e expurgo

Backups contêm dados pessoais dos mesmos titulares que os dados
originais — a política de retenção e expurgo dos backups **não pode ser
mais permissiva** que a política de retenção dos dados na base principal.
Quando um dado é excluído por direito ao esquecimento (ver
`docs/compliance/lgpd.md`), documentar até quando os backups que ainda o
contêm permanecerão retidos (respeitando a retenção mínima legal quando
aplicável) e quando serão expurgados definitivamente.

## Backup da outbox e da idempotência

`outbox_messages` e `processed_events` (ver ADR-034/035) vivem no mesmo
Postgres do resto do sistema, então já entram no backup padrão do banco.
Não recebem tratamento especial de restauração: em caso de restore, mensagens
`Pending` restauradas são reprocessadas normalmente pelo `OutboxProcessorJob`
(idempotência dos consumers, via `processed_events`, evita efeito colateral
duplicado — ADR-035) e registros de idempotência restaurados só encurtam a
janela de reprocessamento, nunca causam inconsistência.

## Política de retenção de dados (resumo)

- Dados de clientes e processos: 5 anos após o último movimento
- Audit logs: 5 anos
- Publicações: indefinidamente
- Backups: alinhados a 5 anos (camada mensal)

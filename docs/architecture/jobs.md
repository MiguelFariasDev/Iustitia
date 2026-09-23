# Jobs recorrentes

Ver também: [ADR-017](../adr/017-hangfire-jobs.md).

## Jobs registrados (Etapa 0.5)

| Job                  | Cron          | O que faz                                                          |
|----------------------|---------------|---------------------------------------------------------------------|
| `OutboxProcessorJob`  | `* * * * *`   | `IOutboxProcessor.ProcessPendingMessagesAsync` — publica a outbox   |
| `OutboxCleanupJob`    | `0 3 * * *`   | `IOutboxCleanupService.CleanupAsync` — remove outbox/idempotência antigas |
| `AuditLogCleanupJob`  | `0 4 * * 0`   | Remove audit logs com mais de 5 anos (retenção legal — nunca antes) |
| `HealthCheckJob`      | `*/5 * * * *` | Verifica Postgres, Redis e tamanho da fila da outbox                |

Habilitação e cron de cada job são configuráveis via appsettings
(seção `Jobs`), sem precisar recompilar — ver
`Platform.Infrastructure.Jobs.JobsOptions`.

## Como criar um novo job

1. Crie a classe em `Platform.Infrastructure/Jobs/Jobs/MeuJob.cs`, recebendo
   suas dependências via construtor (DI normal) e expondo um método
   `RunAsync(CancellationToken)` marcado com `[AutomaticRetry(Attempts = 3)]`.
2. Adicione uma entrada em `JobsOptions` (nome + cron padrão).
3. Registre em `RecurringJobsRegistrar.RegisterAll()`:
   `recurringJobManager.AddOrUpdate<MeuJob>("meu-job", job => job.RunAsync(CancellationToken.None), jobs.MeuJob.CronExpression);`

Módulos de negócio (Legal/Workflow/CNJ polling, ...) seguem o mesmo padrão a
partir da Fase 1+, cada um com sua própria entrada em `JobsOptions`.

## Dashboard do Hangfire

Exposto em `/jobs` pelo `Hosts/Api` (não pelo Worker), protegido por
`HangfireDashboardAuthorizationFilter` — só usuários autenticados com papel
`Admin` (ver `Policies.AdminOnly`). Pode ser desligado via
`Hangfire:DashboardEnabled` (já desligado por padrão no Worker, já que o
dashboard só faz sentido atrás da API autenticada).

Mostra: jobs agendados, execuções em andamento, histórico, falhas e
servidores ativos (útil para confirmar que o Worker está de pé e
processando).

## Monitoramento e alertas

`HealthCheckJob` loga, a cada execução: saúde de Postgres/Redis
(`LogCritical` em falha) e `outbox.pending.count` (`LogWarning` acima de
1000 mensagens pendentes). Em produção, esses logs estruturados (Serilog →
Application Insights, ver `docs/compliance` e Etapa 0.6) alimentam regras de
alerta do Azure Monitor:

- Outbox pendente > 1000 → alerta (mesmo limiar do `HealthCheckJob`).
- `AuditLogCleanupJob`/`OutboxCleanupJob` sem execução bem-sucedida em 24h →
  alerta (job travado/falhando silenciosamente).
- `Hangfire.Server.BackgroundServerProcess` sem heartbeat → Worker caiu.

Essas regras de alerta são configuração de infraestrutura (Azure Monitor
Alert Rules sobre a Application Insights do ambiente), não código — ficam
para a Etapa de provisionamento de infra (`infra/terraform`), fora do escopo
de código desta etapa.

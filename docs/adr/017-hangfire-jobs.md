# ADR-017: Hangfire para jobs agendados

**Status:** Aceito — implementado na Etapa 0.5

## Contexto

Vários processos do sistema precisam rodar em background, sem depender de
uma requisição HTTP: processar a outbox, limpar mensagens antigas, aplicar a
política de retenção de audit logs (5 anos — LGPD/OAB) e verificar a saúde
das dependências externas. Precisamos de um agendador confiável, com
persistência (sobrevive a restart do processo), dashboard operacional e
retry nativo.

## Decisão

Usar **Hangfire** com storage no **PostgreSQL** (mesmo banco físico do
sistema — `Hangfire.PostgreSql`), rodando dentro de `Hosts/Worker` (o
servidor de jobs) e com o dashboard (`/jobs`) exposto por `Hosts/Api`,
protegido por `HangfireDashboardAuthorizationFilter` (só papel `Admin`).

Jobs recorrentes registrados nesta etapa (`RecurringJobsRegistrar`, a partir
da seção `Jobs` do appsettings):

| Job                  | Cron          | Responsabilidade                                   |
|----------------------|---------------|-----------------------------------------------------|
| `OutboxProcessorJob`  | `* * * * *`   | Processa a outbox pendente (ver ADR-034)            |
| `OutboxCleanupJob`    | `0 3 * * *`   | Remove outbox antiga e idempotência expirada        |
| `AuditLogCleanupJob`  | `0 4 * * 0`   | Remove audit logs com retenção legal expirada       |
| `HealthCheckJob`      | `*/5 * * * *` | Verifica Postgres/Redis e tamanho da fila da outbox |

> Hangfire (via NCrontab) só suporta cron de 5 campos — granularidade
> mínima de 1 minuto. O `OutboxProcessorJob` roda a cada minuto, não a cada
> 10 segundos como um cron de 6 campos sugeriria; isso é aceitável dado que
> a entrega já é assíncrona por natureza (Outbox Pattern).

## Consequências

**Positivas:**
- Dashboard operacional pronto (`/jobs`) para visualizar execuções,
  falhas e reprocessar jobs manualmente, sem construir nada do zero.
- Persistência no mesmo Postgres do sistema — sem infraestrutura extra
  (ex.: Redis dedicado só para jobs) além do que já existe.
- `[AutomaticRetry(Attempts = 3)]` em cada job dá retry nativo sem lógica
  própria de backoff no código de negócio.
- Testado de ponta a ponta (Etapa 0.5): Worker sobe, `RecurringJobsRegistrar`
  registra os 4 jobs, `HealthCheckJob` roda e reporta Postgres/Redis
  saudáveis e o tamanho real da fila da outbox.

**Negativas / trade-offs:**
- Acoplamento a cron de granularidade de minuto (ver nota acima) — não serve
  para processamento "quase tempo real"; para isso, o caminho é MassTransit
  (ADR-016), não Hangfire.
- Dashboard exposto na mesma API pública exige atenção redobrada à policy de
  autorização (`AdminOnly`) — um erro de configuração aqui vaza informação
  operacional sensível.

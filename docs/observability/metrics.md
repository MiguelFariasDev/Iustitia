# Métricas

Ver também: [ADR-036](../adr/036-observabilidade-serilog-opentelemetry.md).

## Métricas customizadas (Meter "Advocacia")

| Métrica                     | Tipo             | Unidade | Descrição                                        |
|------------------------------|------------------|---------|---------------------------------------------------|
| `outbox.pending.count`        | ObservableGauge  | —       | Mensagens pendentes na outbox (atualizado pelo HealthCheckJob) |
| `outbox.processed.count`      | Counter          | —       | Mensagens da outbox publicadas com sucesso         |
| `outbox.failed.count`         | Counter          | —       | Mensagens da outbox que falharam ao publicar       |
| `outbox.processing.duration`  | Histogram        | ms      | Duração de um ciclo de processamento da outbox     |
| `jobs.executed.count`         | Counter          | —       | Execuções de jobs recorrentes (tag: `job`)         |
| `jobs.failed.count`           | Counter          | —       | Execuções de jobs recorrentes que falharam (tag: `job`) |
| `jobs.duration`               | Histogram        | ms      | Duração de execução de um job (tag: `job`)         |
| `consumers.processed.count`   | Counter          | —       | Mensagens processadas com sucesso (tag: `consumer`)|
| `consumers.failed.count`      | Counter          | —       | Mensagens que falharam no processamento (tag: `consumer`) |
| `handlers.executed.count`     | Counter          | —       | Commands/Queries executados (tag: `handler`)       |
| `handlers.duration`           | Histogram        | ms      | Duração de execução de um Command/Query (tag: `handler`) |

Mais as métricas automáticas de instrumentação: Runtime (.NET GC, threads),
ASP.NET Core (requisições/duração), HttpClient.

## Como adicionar uma nova métrica

1. Adicione o método em `IAppMetrics` (BuildingBlocks.Application.Abstractions).
2. Implemente em `CustomMetrics` (BuildingBlocks.Infrastructure.Observability),
   criando o `Counter`/`Histogram`/`ObservableGauge` no construtor (o `Meter`
   já existe, um só por processo).
3. Chame `IAppMetrics.MeuMetodo(...)` do código que gera o evento a medir
   (job, consumer, handler, ...).

## Dashboards

Nesta etapa (Fase 0), não há dashboard Azure provisionado — as métricas
ficam disponíveis via `/metrics` (Prometheus, protegido por `AdminOnly`) e,
quando `ApplicationInsights:ConnectionString` estiver configurada, também
no workspace de Application Insights. Provisionamento de dashboard
operacional real (`infra/terraform`) fica para quando a infra de produção
for de fato criada.

# ADR-036: Serilog + OpenTelemetry + Application Insights

**Status:** Aceito — implementado na Etapa 0.6

## Contexto

Depois de completar Outbox/mensageria/jobs (Etapa 0.5), o sistema já tem
processos assíncronos rodando fora do fluxo de uma requisição HTTP —
diagnosticar um problema sem logs estruturados, correlação entre serviços e
métricas de saúde fica cada vez mais difícil. Precisamos de uma pilha de
observabilidade única para Api e Worker: logs, traces e métricas.

## Decisão

Três peças complementares, nunca sobrepostas:

1. **Serilog** — logging estruturado (JSON possível, texto legível em
   Console por padrão). `AddAdvocaciaSerilog` (BuildingBlocks.Infrastructure/
   Observability/SerilogConfiguration.cs) configura: `ReadFrom.Configuration`
   (nível mínimo e overrides por namespace vêm do appsettings, seção
   "Serilog"), enrichers (`CorrelationIdEnricher`, `TenantIdEnricher`,
   `UserIdEnricher`, `SensitiveDataMaskingEnricher` — ver ADR-037),
   `MachineName`/`ThreadId`, e um filtro que exclui logs de `/health/*` (alto
   volume, baixo valor de diagnóstico). Sink de Application Insights só é
   ligado se `ApplicationInsights:ConnectionString` estiver configurada.
2. **OpenTelemetry** — traces (ASP.NET Core, HttpClient, e os
   `ActivitySource` customizados: Outbox, Jobs, Handlers, Consumers — ver
   `ActivitySourceNames`) e métricas (Runtime, ASP.NET Core, HttpClient, e o
   `Meter` customizado "Advocacia" — ver `MeterNames`/`CustomMetrics`).
   `AddAdvocaciaObservability` (OpenTelemetryConfiguration.cs) registra tudo
   isso e expõe as métricas via `/metrics` (Prometheus,
   `OpenTelemetry.Exporter.Prometheus.AspNetCore`).
3. **Azure Monitor (Application Insights)** — via
   `Azure.Monitor.OpenTelemetry.AspNetCore`, só habilitado quando
   `ApplicationInsights:ConnectionString` está presente (nunca falha o
   startup por falta de um recurso Azure que ainda não existe nesta fase).

Métricas customizadas expostas (`IAppMetrics`/`CustomMetrics`):
`outbox.pending.count` (gauge), `outbox.processed.count`,
`outbox.failed.count` (counters), `outbox.processing.duration`,
`jobs.executed.count`, `jobs.failed.count`, `jobs.duration`,
`consumers.processed.count`, `consumers.failed.count`,
`handlers.executed.count`, `handlers.duration`.

`IAppMetrics` vive em `BuildingBlocks.Application.Abstractions` (não em
Infrastructure) para que a Application layer (ex.: um futuro
`MetricsBehavior` do MediatR) possa usá-la sem violar a regra de dependência
— a implementação real (`CustomMetrics`, baseada em
`System.Diagnostics.Metrics`) fica em BuildingBlocks.Infrastructure.

## Consequências

**Positivas:**
- Testado de ponta a ponta (Etapa 0.6): Api sobe (bare-metal e via Docker),
  loga em Console estruturado, publica métricas em `/metrics` (protegido por
  `AdminOnly`), e nenhuma falha de startup mesmo sem Application Insights
  configurado.
- `OutboxProcessor`/`ConsumerBase` já emitem spans e métricas — qualquer
  novo consumer/job herda essa instrumentação automaticamente (ver
  ConsumerBase/OutboxProcessorJob).
- Correlação entre logs e traces é gratuita: o `Activity.Current` do
  OpenTelemetry e o `LogEnrichmentContext.CorrelationId` (ADR-037) carregam
  o mesmo tipo de identificador ao longo do fluxo.

**Negativas / trade-offs:**
- Sem Application Insights real configurado (ambiente atual), a validação
  de exportação de telemetria fica limitada ao console/`/metrics` — não foi
  possível verificar contra um workspace Azure real nesta etapa.
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` ainda está em beta (sem
  release estável na época desta etapa) — aceitável porque o scraping
  Prometheus/Grafana é, por ora, só para uso interno de operação, não uma
  dependência crítica do produto.

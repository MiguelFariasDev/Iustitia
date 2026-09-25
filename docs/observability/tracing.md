# Tracing

Ver também: [ADR-036](../adr/036-observabilidade-serilog-opentelemetry.md).

## ActivitySource customizados

| Nome (`ActivitySourceNames`) | Onde é usado                                  |
|-------------------------------|------------------------------------------------|
| `Advocacia.Outbox`             | `OutboxProcessor` — um span por lote processado |
| `Advocacia.MassTransit.Consumers` | `ConsumerBase<TMessage>` — um span por mensagem consumida |
| `Advocacia.Jobs`                | Reservado para jobs Hangfire (ver docs/architecture/jobs.md) |
| `Advocacia.Handlers`            | Reservado para handlers MediatR                |

Instrumentação automática (`OpenTelemetryConfiguration`): ASP.NET Core
(requisições HTTP, exceto `/health/*`) e HttpClient (chamadas externas).

## Como adicionar um novo span

```csharp
private static readonly ActivitySource ActivitySource = new(ActivitySourceNames.Jobs);

using var activity = ActivitySource.StartActivity("meu-job.executar");
activity?.SetTag("meu.atributo", valor);
```

`StartActivity` retorna `null` quando não há nenhum listener registrado
(custo praticamente zero) — sempre use `activity?.` para as chamadas
seguintes, nunca assuma que não é nulo.

## Correlação entre logs e traces

O `CorrelationId` (ver ADR-037, `LogEnrichmentContext`) e o
`Activity.Current`/trace do OpenTelemetry cobrem o mesmo tipo de
necessidade (rastrear um fluxo de ponta a ponta) por caminhos distintos —
`CorrelationId` é o identificador único para correlacionar LOGS (aparece em
toda linha de log via `CorrelationIdEnricher`); o trace do OpenTelemetry
correlaciona SPANS (visualizável em Application Insights/Jaeger). Os dois
convivem sem conflito: um span pode (e deve, quando fizer sentido) receber
`activity?.SetTag("correlation.id", LogEnrichmentContext.CorrelationId)`
para navegar de um trace para os logs correspondentes.

# Health checks

Ver também: [ADR-036](../adr/036-observabilidade-serilog-opentelemetry.md).

## Endpoints (Hosts/Api)

| Endpoint       | Verifica dependências? | Uso                                          |
|----------------|-------------------------|-----------------------------------------------|
| `/health/live`  | Não                      | Liveness probe — só confirma que o processo está de pé |
| `/health/ready` | Sim (tag `ready`)        | Readiness probe — 200 se tudo OK, 503 se alguma dependência falhar |
| `/health`       | Sim (todos os checks)    | JSON estruturado com detalhes de cada check — uso operacional/debug |

## Checks registrados (`HealthChecksConfiguration.AddAdvocaciaHealthChecks`)

- **postgres** (`AspNetCore.HealthChecks.NpgSql`) — conexão com o banco.
- **redis** (`AspNetCore.HealthChecks.Redis`) — conexão com o cache.
- **masstransit-bus** — registrado automaticamente pelo MassTransit
  (`AddAdvocaciaMessaging`) assim que `AddHealthChecks()` já foi chamado;
  cobre tanto RabbitMQ (dev) quanto Azure Service Bus (prod), sem código
  próprio.
- **outbox** (`OutboxHealthCheck`) — `Degraded` quando a mensagem `Pending`
  mais antiga já espera há mais de `HealthChecks:OutboxPendingThresholdMinutes`
  (padrão 30min); nunca `Unhealthy` — um backlog na outbox não deve tirar a
  aplicação de rotação de tráfego.

Estruturas prontas, ainda não registradas em nenhum grupo (aguardando as
integrações correspondentes): `AnthropicHealthCheck` (Fase 3),
`CnjHealthCheck` (Fase 1).

## Como adicionar um novo check

```csharp
public sealed class MeuCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // ...
    }
}
```

Registre em `HealthChecksConfiguration.AddAdvocaciaHealthChecks`:
`.AddCheck<MeuCheck>("meu-check", tags: ["ready"])` (ou sem a tag `"ready"`
se não deve gatilhar `/health/ready`, só aparecer em `/health`).

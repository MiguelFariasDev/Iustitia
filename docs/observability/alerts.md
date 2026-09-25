# Alertas

Ver também: [ADR-036](../adr/036-observabilidade-serilog-opentelemetry.md),
[docs/architecture/jobs.md](../architecture/jobs.md).

## Alertas planejados (Application Insights / Azure Monitor)

| Alerta                                         | Limiar             | Severidade |
|-------------------------------------------------|---------------------|------------|
| Outbox pendente                                  | `outbox.pending.count` > 1000 | Crítico |
| Jobs falhados                                    | `jobs.failed.count` > 10 em 1h | Crítico |
| Consumers falhados                               | `consumers.failed.count` > 50 em 1h | Crítico |
| Dead letter queue                                | > 100 mensagens | Crítico |
| Duração de handlers                              | P95 de `handlers.duration` > 2s | Warning |
| Taxa de erro HTTP                                | > 5% das requisições | Warning |
| `/health/ready` falhando                          | qualquer falha | Crítico |

## Estado atual (Fase 0)

Estas regras ainda **não estão provisionadas** como Azure Monitor Alert
Rules — isso é configuração de infraestrutura (`infra/terraform`), não
código, e depende de um workspace de Application Insights real já existir.
O que já existe nesta etapa é a fonte de dados para essas regras:

- `HealthCheckJob` (ver docs/architecture/jobs.md) já loga `LogCritical`
  quando Postgres/Redis falham, e `LogWarning` quando a outbox pendente
  ultrapassa o limiar — esses logs, uma vez em Application Insights, viram
  a base de uma regra de alerta por contagem de eventos.
- As métricas customizadas (`outbox.pending.count`, `jobs.failed.count`,
  `consumers.failed.count`, ...) já são exportadas via `/metrics` e (quando
  configurado) Application Insights — uma regra de alerta por métrica
  aponta diretamente para elas, sem precisar de nenhum código adicional.
- `/health/ready` (ver health-checks.md) já retorna 503 quando alguma
  dependência crítica falha — uma regra de "availability test" do Azure
  Monitor apontando para esse endpoint cobre o último item da lista.

## Próximo passo

Quando a infra de produção for provisionada (fora do escopo de código desta
fase), criar essas regras via Terraform (`infra/terraform`), apontando os
canais de ação (e-mail/Teams/Slack) para quem for responsável pelo
plantão operacional.

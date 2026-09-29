# Módulo Legal — Visão Geral

**Status:** em construção. A Etapa 1.2 (captura e persistência de
publicações) está implementada; clientes e processos chegam na Fase 2.

## O que é

O módulo **Legal** é o bounded context do domínio jurídico do Iustitia: o que
é específico da prática da advocacia, em oposição ao que sustenta o sistema
(identidade, tenancy, auditoria, jobs — esses vivem em `Platform`).

Conceitos do módulo:

| Conceito | Fase | Status |
|---|---|---|
| **Publicação** — comunicação capturada de um diário oficial | 1 | implementado |
| **Cliente** — pessoa física ou jurídica atendida pelo escritório | 2 | pendente |
| **Processo** — processo judicial acompanhado | 2 | pendente |
| **Sugestão** — classificação e prazo propostos por IA, sempre com revisão humana | 3 | pendente |

A decisão de criar o módulo como bounded context separado, e as regras de
dependência que a sustentam, estão na **ADR-055**.

## Camadas

```
/src/Modules/Legal
  /Legal.Domain
      /Publications      Publication (agregado), CNJNumber, RawContent,
                         PublishedAt, PublicationType/Status/Source
      /Events            PublicationCapturedEvent, PublicationStatusChangedEvent
      /Specifications    consultas reutilizáveis (por tenant, por processo, fila de revisão)
  /Legal.Application
      /Abstractions      IPublicationRepository, ILegalUnitOfWork
      /Features          CapturePublication, GetPublicationById,
                         ListPublications, MarkPublicationAsRead
      /Mappings          PublicationMappingProfile (Mapster)
  /Legal.Infrastructure
      /Persistence       LegalDbContext, configurações Fluent API,
                         PublicationRepository, LegalUnitOfWork, migrations
      /Jobs              CnjCaptureJob, CnjCaptureJobOptions, registrar recorrente
      /Observability     CnjCaptureTelemetry (spans + métricas)
  /Legal.Api             endpoints — Fase 2
```

Regra de dependência (verificada por `LegalModuleConventionTests`):

```
Legal.Domain         -> BuildingBlocks.Domain
Legal.Application    -> Legal.Domain + BuildingBlocks.Application
Legal.Infrastructure -> Legal.Application + BuildingBlocks.Infrastructure
                        + Integrations.CNJ.Application   (a porta, não a implementação)
```

**Nenhuma camada de Legal depende de `Platform.*`.** O módulo consome a
plataforma por `BuildingBlocks`.

## Persistência

`LegalDbContext` herda de `BaseDbContext`, ganhando automaticamente:

- filtro global de **soft delete** (`ISoftDeletable`);
- filtro global de **isolamento por tenant** (`IHasTenant`), reforçado por
  Row-Level Security no Postgres (ver ADR-004 e
  `infra/supabase/migrations/011_publications_rls.sql`);
- descoberta das configurações Fluent API do assembly.

Além de `publications`, o contexto mapeia `outbox_messages` — **excluída das
migrations de Legal**, porque a tabela pertence ao Platform. O mapeamento
existe para que o `OutboxInterceptor` grave os eventos de domínio de Legal na
mesma transação do `SaveChanges` (ver ADR-008).

## Fronteira transacional

Legal tem sua própria: **`ILegalUnitOfWork`**. O `IUnitOfWork` registrado por
padrão no container resolve para o `PlatformDbContext`, e um handler de Legal
que o injetasse salvaria no contexto errado, sem erro visível. Ver ADR-055
para a discussão completa, incluindo a interação com o `TransactionBehavior`
do MediatR.

## Registro no host

`AddLegalInfrastructure` precisa ser chamado **depois** de
`AddPlatformInfrastructure`, e o host precisa registrar também
`AddCnjIntegration` — o job de captura depende de `ICnjClient`, e o módulo
referencia apenas a porta:

```csharp
builder.Services.AddPlatformInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddCnjIntegration(builder.Configuration);
builder.Services.AddLegalInfrastructure(builder.Configuration);
```

No Worker, os jobs recorrentes do módulo são registrados por
`LegalRecurringJobsRegistrar`, ao lado do registrar do Platform.

## Documentos relacionados

- [publications.md](publications.md) — modelo de domínio, ciclo de vida, índices
- [cnj-capture.md](cnj-capture.md) — fluxo do job, paginação, erros, observabilidade
- ADR-055 — Módulo Legal como bounded context
- ADR-056 — Deduplicação por ExternalId
- ADR-057 — Estratégia de índices para publications

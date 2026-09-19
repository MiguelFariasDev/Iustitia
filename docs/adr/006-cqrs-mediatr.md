# ADR-006: CQRS pragmático com MediatR

**Status:** Aceito

## Contexto

Dentro de cada módulo, as operações de escrita (criar tenant, convidar usuário,
revisar publicação) têm necessidades muito diferentes das operações de
leitura (listar processos paginados, buscar auditoria por período): escrita
precisa de validação, invariantes de domínio e transação; leitura precisa de
projeções eficientes, muitas vezes sem passar pelas entidades de domínio
completas. Modelar as duas coisas com a mesma abstração tende a forçar
compromissos ruins dos dois lados (DTOs de leitura carregando comportamento de
escrita, ou entidades de escrita expondo formas de consulta que não deveriam).

Um CQRS "forte" (stores de leitura e escrita fisicamente separados,
sincronizados por eventos) seria overkill para o estágio atual do produto e
multiplicaria a complexidade operacional sem necessidade comprovada.

## Decisão

Adotar **CQRS pragmático**: um único banco físico, mas commands e queries
tratados como conceitos distintos no código, orquestrados pelo **MediatR**:

- **Commands** (`ICommand`/`ICommand<TResponse>`, ver
  `BuildingBlocks.Application.Messaging`) — sempre passam por
  `AggregateRoot`/regras de domínio, usam EF Core via `IRepository`/`IUnitOfWork`.
- **Queries** (`IQuery<TResponse>`) — podem usar EF Core com `AsNoTracking`
  ou Dapper diretamente para relatórios/listagens que não justificam
  carregar o grafo de entidades completo.
- Cada caso de uso vive em sua própria pasta Vertical Slice
  (`Features/{Grupo}/{CasoDeUso}/`), com `Command`/`Query`, `Handler`,
  `Validator` (FluentValidation) e `Response` juntos — ver ADR-005.
- Pipeline behaviors do MediatR (`ValidationBehavior`, `LoggingBehavior`,
  `PerformanceBehavior`, `TransactionBehavior`) aplicam cross-cutting
  concerns sem que cada handler precise repeti-los.

## Consequências

**Positivas:**
- Separação clara de responsabilidades sem o custo operacional de bancos de
  leitura/escrita separados.
- Pipeline behaviors centralizam validação, logging, performance e
  transação — handlers ficam focados só na regra de negócio.
- Fácil evoluir para CQRS "forte" no futuro (ex.: read model dedicado para
  relatórios pesados) sem reescrever a camada de Application, já que
  Commands e Queries já são conceitos separados no código.

**Negativas / trade-offs:**
- Ainda existe apenas um banco físico — leituras pesadas competem por
  recursos com escritas (mitigável com réplicas de leitura do Postgres no
  futuro, se necessário).
- Overhead de um Command/Query por caso de uso é maior do que um único
  serviço "CRUD genérico" — aceito em troca de testabilidade e clareza.

## Atualização — Etapa 0.4 (pipeline de behaviors implementado)

Os quatro behaviors citados acima — só registrados como placeholders desde
a Etapa 0.1 — foram implementados de verdade nesta etapa (ver ADR-030 para
o detalhamento de cada um). Registro e composição raiz de MediatR/
FluentValidation/Mapster migraram de `Platform.Api` para
`Platform.Application.DependencyInjection.AddPlatformApplication` — o
registro de MediatR é uma preocupação de Application, não de apresentação
(Swagger/OpenAPI é a única coisa que resta em `Platform.Api.AddPlatformApi`).

Todos os handlers de Platform (Auth, Tenants, Users, Audit, Settings,
FeatureFlags) retornam erros via `ErrorFactory.From(ErrorCode.X)` — ver
ADR-032 (Catálogo de Erros).

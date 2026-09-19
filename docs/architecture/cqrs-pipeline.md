# Pipeline de CQRS (MediatR)

> Ver ADR-006 (CQRS pragmático) e ADR-030 (pipeline de behaviors) para o
> racional completo das decisões descritas aqui.

## Diagrama do pipeline

```
HTTP Request
     │
     ▼
Minimal API endpoint (Platform.Api/Endpoints/*)
     │  cria o Command/Query, chama ISender.Send
     ▼
┌─────────────────────────────────────────────────────────────┐
│ LoggingBehavior            loga início; loga fim com duração │
│  └─ ValidationBehavior     FluentValidation; curto-circuita  │
│      └─ PerformanceBehavior   Stopwatch; warning se lento    │
│          └─ TransactionBehavior  só Commands; Begin/Commit/  │
│              │                    Rollback via IUnitOfWork   │
│              ▼                                                │
│           Handler (regra de negócio, Result/Result<T>)       │
└─────────────────────────────────────────────────────────────┘
     │
     ▼
ResultExtensions.ToHttpResult (Platform.Api/Extensions)
     │  sucesso → 200/201/204; falha → ProblemDetails (RFC 7807)
     ▼
HTTP Response
```

O registro em `Platform.Application.DependencyInjection.AddPlatformApplication`
define a ordem (o primeiro `AddOpenBehavior` é o mais externo):

```csharp
mediatrConfiguration.AddOpenBehavior(typeof(LoggingBehavior<,>));
mediatrConfiguration.AddOpenBehavior(typeof(ValidationBehavior<,>));
mediatrConfiguration.AddOpenBehavior(typeof(PerformanceBehavior<,>));
mediatrConfiguration.AddOpenBehavior(typeof(TransactionBehavior<,>));
```

## O que cada behavior faz (resumo — ver ADR-030 para o detalhamento)

| Behavior | Responsabilidade | Aplica-se a |
|---|---|---|
| `LoggingBehavior` | Loga request name, `user_id`/`tenant_id`, `correlation_id`, início/fim com duração. | Commands e Queries |
| `ValidationBehavior` | Roda todos os `IValidator<TRequest>`; curto-circuita com `Result.Failure(ErrorFactory.From(...))` se inválido. | Commands e Queries |
| `PerformanceBehavior` | Mede duração; loga warning acima do limiar configurável (`Performance:ThresholdMilliseconds`). | Commands e Queries |
| `TransactionBehavior` | Abre transação antes do handler; commit se `Result.IsSuccess`, rollback caso contrário (ou em exceção). | Só Commands (`ICommandBase`) |

## Como criar um novo behavior

1. Implemente `IPipelineBehavior<TRequest, TResponse>` em
   `BuildingBlocks.Application/Behaviors/`.
2. Registre com `mediatrConfiguration.AddOpenBehavior(typeof(SeuBehavior<,>))`
   em `AddPlatformApplication`, na posição correta da cadeia (mais cedo =
   mais externo).
3. Se o behavior precisa de configuração via appsettings, crie uma classe
   `SeuBehaviorOptions` (ver `PerformanceBehaviorOptions` como modelo) e
   registre com `services.Configure<SeuBehaviorOptions>(configuration.GetSection(...))`.
4. Escreva testes unitários isolados (ver `BuildingBlocks.Application.UnitTests/Behaviors/`)
   chamando `behavior.Handle(request, next, cancellationToken)` diretamente,
   sem precisar de um host HTTP real.
5. Se o comportamento precisa ser validado de ponta a ponta (ex.: "está
   mesmo conectado no pipeline real"), adicione um teste em
   `Platform.IntegrationTests/PipelineTests.cs`.

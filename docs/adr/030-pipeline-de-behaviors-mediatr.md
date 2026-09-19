# ADR-030: Pipeline de Behaviors do MediatR

**Status:** Aceito

## Contexto

Toda Command/Query precisa de um conjunto comum de preocupações
transversais — validar a entrada, logar início/fim, medir performance e
garantir atomicidade — sem que cada handler tenha que reimplementá-las. A
Etapa 0.1 já registrava quatro `IPipelineBehavior<,>` no MediatR
(`ValidationBehavior`, `LoggingBehavior`, `PerformanceBehavior`,
`TransactionBehavior`), mas como placeholders (`return next()` sem
nenhuma lógica) — ver ADR-006.

## Decisão

Implementar os quatro behaviors de verdade, registrados nesta ordem (o
primeiro registrado é o mais externo do pipeline):

```
Request
  └─ LoggingBehavior       (loga início; loga fim com duração)
      └─ ValidationBehavior   (FluentValidation; curto-circuita se inválido)
          └─ PerformanceBehavior  (Stopwatch; warning se > threshold)
              └─ TransactionBehavior  (só Commands; Begin/Commit/Rollback)
                  └─ Handler (regra de negócio)
Response
```

### LoggingBehavior

Loga nome da request, `user_id`/`tenant_id` (via `ICurrentUser`) e um
`correlation_id` — tomado de `Activity.Current?.Id` (W3C trace context, já
propagado pelo ASP.NET Core por requisição), não de `HttpContext`
diretamente, para `BuildingBlocks.Application` não ganhar uma dependência
de `Microsoft.AspNetCore.Http`. Loga início, fim com duração
(`Stopwatch`), e erro com stack trace se a request lançar uma exceção
(nunca loga o corpo da request, que pode conter senha/token).

### ValidationBehavior

Resolve todos os `IValidator<TRequest>` registrados, valida, e se houver
falha constrói um `Result`/`Result<T>` de falha (nunca lança exceção) —
ver ADR-031 para o mapeamento FluentValidation → `ErrorCode`.
`ValidatorOptions.Global.DefaultRuleLevelCascadeMode`/
`DefaultClassLevelCascadeMode = CascadeMode.Stop` (configurado em
`AddPlatformApplication`) garante que só a primeira falha do objeto
inteiro chega ao behavior — necessário para o mapeamento 1:1 de uma única
`ValidationFailure` para um único `ErrorCode`.

### PerformanceBehavior

Mede a duração com `Stopwatch` e loga um `warning` se ultrapassar o
limiar configurável (appsettings, seção `Performance:ThresholdMilliseconds`,
padrão 500ms — ver `PerformanceBehaviorOptions`). Nunca interrompe a
execução, é só um sinal de observabilidade.

### TransactionBehavior

Só se aplica a `ICommandBase` (marcador comum a `ICommand`/`ICommand<T>`,
ver `BuildingBlocks.Application.Messaging`) — Queries nunca abrem
transação. Abre a transação via `IUnitOfWork.BeginTransactionAsync` antes
do handler, e decide commit/rollback a partir de `Result.IsSuccess`
(nunca de uma exceção, já que erros de negócio nunca lançam — ver
ADR-007): sucesso → `CommitTransactionAsync`; falha ou exceção →
`RollbackTransactionAsync`. `Result<TValue>` herda de `Result`, então o
cast `(Result)(object)response` para ler `IsSuccess` é seguro para
qualquer `TResponse` de um `ICommandBase` (sempre `Result` ou
`Result<T>`). `IUnitOfWork.BeginTransactionAsync` já é idempotente (não
abre uma segunda transação se uma já estiver ativa no mesmo escopo — ver
`UnitOfWork`), então handlers que chamassem outro `Send` internamente
(não é o caso hoje) não aninhariam transações.

## Consequências

**Positivas:**
- Handlers ficam livres de código repetitivo de validação/log/transação —
  só a regra de negócio.
- Ordem do pipeline é explícita e documentada (registro = ordem de
  execução no MediatR), fácil de raciocinar e de estender com um novo
  behavior no futuro.
- `TransactionBehavior` nunca precisa saber qual `ErrorType`/`ErrorCode`
  causou a falha — só olha `IsSuccess`, mantendo o behavior genérico.

**Negativas / trade-offs:**
- `CascadeMode.Stop` global (necessário para o mapeamento de erro 1:1)
  significa que um Command com múltiplos campos inválidos ao mesmo tempo
  só reporta o primeiro erro encontrado por vez — o cliente precisa
  corrigir e reenviar para descobrir o próximo. Aceito como trade-off
  consciente: a alternativa (agregar múltiplos `ErrorCode` num único
  `Error`) exigiria mudar o shape de `Error`/`Result`, um custo maior.
- `TransactionBehavior` fazendo cast `(Result)(object)response` é uma
  simplificação segura *hoje* (todo `ICommandBase` retorna `Result`/
  `Result<T>`), mas quebraria silenciosamente se um novo tipo de request
  não seguisse esse contrato — mitigado por `ICommandBase` ser a única
  forma de marcar algo como Command, e por `ICommand`/`ICommand<T>` serem
  as únicas implementações dessa interface no código.

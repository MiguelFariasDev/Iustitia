# Estrutura de uma Feature (Vertical Slice)

> Ver ADR-005 (Clean Architecture + Vertical Slice) e ADR-006 (CQRS) para o
> racional completo.

## Padrão de pastas

Cada caso de uso vive na sua própria pasta, agrupado por área de negócio,
dentro de `src/Platform/Platform.Application/Features/`:

```
Features/
  {Area}/                      ex.: Tenants, Users, Audit, Settings, FeatureFlags, Auth
    {CasoDeUso}/                ex.: CreateTenant, InviteUser, GetSettings
      {CasoDeUso}Command.cs      (ou Query.cs, se for leitura)
      {CasoDeUso}Handler.cs
      {CasoDeUso}Validator.cs    (Commands quase sempre têm; Queries simples às vezes não)
      {CasoDeUso}Response.cs
```

Tudo que um caso de uso precisa (contrato de entrada, regra de execução,
validação, contrato de saída) fica junto, na mesma pasta — não espalhado
em camadas horizontais (`Commands/`, `Handlers/`, `Validators/` como
pastas de topo). Isso é o que "Vertical Slice" quer dizer aqui.

## Padrão de um Command

```csharp
public sealed record CreateTenantCommand(
    string Name, string Cnpj, string AdminName, string AdminEmail, string AdminPassword)
    : ICommand<CreateTenantResponse>;
```

- `record`, imutável.
- Implementa `ICommand<TResponse>` (retorna algo em caso de sucesso) ou
  `ICommand` (só sucesso/falha, sem valor) — ver
  `BuildingBlocks.Application.Messaging`.

## Padrão de uma Query

```csharp
public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<GetTenantByIdResponse>;
```

- Sempre implementa `IQuery<TResponse>` — toda Query retorna algo.

## Padrão de um Handler

```csharp
public sealed class CreateTenantHandler(
    ISupabaseAuthService supabaseAuthService,
    IRepository<Tenant, TenantId> tenantRepository,
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService) : ICommandHandler<CreateTenantCommand, CreateTenantResponse>
{
    public async Task<Result<CreateTenantResponse>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // 1. Validar/criar Value Objects de domínio (retornam Result — propague a falha).
        // 2. Carregar entidades existentes via IRepository, se necessário.
        // 3. Chamar métodos de domínio (nunca mutar campos diretamente).
        // 4. repository.Add(entidade) para entidades novas.
        // 5. unitOfWork.SaveChangesAsync(cancellationToken).
        // 6. auditService.RecordAsync(...) para ações sensíveis.
        // 7. Retornar o Response (ou mapper.Map<Response>(entidade), ver Mapping/).
    }
}
```

Regras:
- Injeção de dependências só via construtor primário (nunca `IServiceProvider`).
- Nunca lança exceção para erro de negócio — sempre `Result.Failure(ErrorFactory.From(ErrorCode.X))`
  (ver `docs/errors/conventions.md`, ADR-032).
- Sempre recebe e propaga `CancellationToken`.
- `TransactionBehavior` (ver `docs/architecture/cqrs-pipeline.md`) já
  cuida do commit/rollback ao redor do handler — o handler só precisa
  chamar `SaveChangesAsync` normalmente, sem `Begin`/`Commit` manual.

## Padrão de um Validator

```csharp
public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress();
        // ...
    }
}
```

Descoberto automaticamente por `AddValidatorsFromAssembly` — não precisa
registrar manualmente. Só valida **forma** da entrada (campos obrigatórios,
formato, tamanho) — regras de negócio (duplicidade, invariantes de
estado) ficam no handler/domínio, nunca no validator.

## Padrão de um Response

```csharp
public sealed record CreateTenantResponse(Guid TenantId, Guid AdminUserId);
```

- `record`, só dados — nunca comportamento.
- Para respostas que espelham uma entidade quase por completo (ex.:
  `GetTenantByIdResponse`), prefira um profile de Mapster
  (`Platform.Application/Mapping/`) a construir manualmente — ver
  exemplos em `TenantMappingProfile`/`UserMappingProfile`. Para
  respostas que são só um recorte pequeno e computado (ex.:
  `UpdateTenantResponse(Guid TenantId, string Name)`), construir
  manualmente continua sendo a opção mais simples e clara.

## Como criar um novo handler/validator

1. Crie a pasta `Features/{Area}/{CasoDeUso}/`.
2. Escreva o `Command`/`Query` primeiro — ele é o contrato público do
   caso de uso.
3. Escreva o `Response`.
4. Escreva o `Handler`, usando os padrões acima.
5. Escreva o `Validator`, se o Command/Query tiver campos que precisem de
   validação de forma.
6. Conecte um endpoint em `Platform.Api/Endpoints/` que só chama
   `ISender.Send(command)` e converte o `Result` via
   `ResultExtensions.ToHttpResult` — nunca lógica de negócio no endpoint
   (ver `EndpointConventionTests`, em `ArchitectureTests`).
7. Escreva testes: `{CasoDeUso}HandlerTests.cs` (unitário, NSubstitute
   para as dependências) e, se o caso de uso tiver uma regra de negócio
   ou fluxo importante o suficiente, um teste de integração em
   `Platform.IntegrationTests/`.

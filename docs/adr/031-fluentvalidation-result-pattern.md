# ADR-031: FluentValidation integrado ao Result Pattern

**Status:** Aceito

## Contexto

Toda entrada de Command/Query precisa ser validada antes de chegar ao
handler (campos obrigatórios, formato de e-mail, tamanho de string,
enums válidos, ...). O ecossistema .NET tem duas abordagens comuns para
isso: `DataAnnotations` (atributos no próprio DTO) ou **FluentValidation**
(regras declarativas separadas do DTO, mais expressivas para regras
condicionais/customizadas). Além de *como* validar, é preciso decidir
*como o resultado da validação vira um erro de negócio* — o projeto já
usa o Result Pattern (ADR-007) para nunca lançar exceção em erros
esperados, e a validação de entrada não é exceção a essa regra.

## Decisão

Usar **FluentValidation**, com um `AbstractValidator<TCommand>`/
`AbstractValidator<TQuery>` por Command/Query (ver Vertical Slice, ADR-005),
descoberto automaticamente via `AddValidatorsFromAssembly` em
`Platform.Application.AddPlatformApplication`. A execução acontece dentro
de `ValidationBehavior` (ver ADR-030), nunca manualmente dentro do
handler — um handler nunca chama `validator.Validate(...)` ele mesmo.

**Conversão para o catálogo de erros:** `ValidationFailure.ErrorCode`
(preenchido pelo FluentValidation com o nome do validador interno, ex.:
`"NotEmptyValidator"`) é mapeado para um `ErrorCode` do grupo
`VALIDATION_*` (ver ADR-032) — mapeamento verificado empiricamente contra
FluentValidation 11.11 (não documentado oficialmente como parte estável
da API pública, então pode mudar em major versions futuras):

| `ValidationFailure.ErrorCode` | `ErrorCode` |
|---|---|
| `NotEmptyValidator`, `NotNullValidator` | `VALIDATION_REQUIRED_FIELD` |
| `EmailValidator` | `VALIDATION_INVALID_FORMAT` |
| `MaximumLengthValidator` | `VALIDATION_MAX_LENGTH_EXCEEDED` |
| `MinimumLengthValidator` | `VALIDATION_MIN_LENGTH_NOT_MET` |
| `GreaterThanValidator`, `LessThanValidator`, `GreaterThanOrEqualValidator`, `LessThanOrEqualValidator` | `VALIDATION_OUT_OF_RANGE` |
| `EnumValidator` | `VALIDATION_INVALID_ENUM` |
| Qualquer outro (`PredicateValidator`/`Must`, customizado) | `VALIDATION_INVALID_FORMAT` |

**Mensagens em pt-BR:** `ValidatorOptions.Global.LanguageManager.Culture`
é fixado para `pt-BR` explicitamente em `AddPlatformApplication` — o
FluentValidation já traz mensagens padrão localizadas para várias
culturas (incluindo pt-BR) para os validadores nativos (`NotEmpty`,
`EmailAddress`, `MaximumLength`, ...), então não foi necessário escrever
mensagens customizadas para os casos comuns. `ErrorFactory.From` usa o
`PropertyName` da falha como argumento da mensagem-molde do
`ErrorCode` (ex.: `"O campo '{0}' é obrigatório."`), preservando qual
campo falhou sem depender da mensagem específica do FluentValidation.

**`CascadeMode.Stop` (nível de regra e de classe):** configurado
globalmente, garante que uma única `ValidationFailure` chega ao
`ValidationBehavior` por execução — necessário porque um `Error` só
carrega um `ErrorCode`/mensagem por vez (ver ADR-030, seção
"Negativas/trade-offs", para o trade-off aceito).

## Consequências

**Positivas:**
- Validators ficam desacoplados dos Commands/Queries (não são atributos
  no próprio record) e são testáveis isoladamente (ver
  `tests/.../Features/ValidatorTests.cs`).
- A conversão para `ErrorCode` dá ao cliente da API um código estável e
  categorizado (`VALIDATION_REQUIRED_FIELD`, não uma string livre do
  FluentValidation), consistente com todo o resto do catálogo de erros.

**Negativas / trade-offs:**
- O mapeamento de `ValidationFailure.ErrorCode` para `ErrorCode` depende
  de um detalhe de implementação do FluentValidation (o nome do
  validador interno) que não é parte da API pública documentada —
  coberto por testes (`ValidationBehaviorTests`) que quebrariam
  visivelmente se uma atualização de major version renomeasse esses
  validadores internos.
- Regras de validação verdadeiramente compostas (que dependem de mais de
  um campo, ou de estado externo como "esse e-mail já existe?") ainda
  precisam ser resolvidas no handler via regra de domínio + `ErrorFactory`
  — FluentValidation aqui cobre só validação de forma/formato da entrada,
  nunca invariantes de negócio.

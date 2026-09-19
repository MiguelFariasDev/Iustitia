# ADR-007: Result Pattern em vez de exceptions de negócio

**Status:** Aceito

## Contexto

Erros de negócio (CNPJ inválido, e-mail duplicado, tenant já suspenso) não
são situações excepcionais — são resultados esperados e frequentes de
validar entrada de usuário e invariantes de domínio. Usar exceptions para
esse fluxo tem custos reais: exceptions em .NET são caras em performance
quando lançadas com frequência, o stack trace captura contexto que não
interessa para um erro de validação, e o fluxo de controle "try/catch em
todo lugar" torna o código de Application difícil de ler e de testar.
Exceptions deveriam sinalizar o INESPERADO (falha de infraestrutura, bug),
não o esperado.

## Decisão

Usar o **Result Pattern** (`Result`, `Result<TValue>`, `Error`, `ErrorType`
— ver `BuildingBlocks.Domain.Results`) para **todo** erro de negócio:

- Métodos de fábrica de entidades (`Tenant.Create`, `User.Invite`) e métodos
  de comportamento (`Tenant.Suspend`, `User.Activate`) retornam
  `Result`/`Result<T>` em vez de lançar exceptions quando uma regra de
  negócio é violada.
- `Error` carrega um `Code` estável (ex.: `"tenant.name.required"`), uma
  `Message` amigável e um `ErrorType` (`Validation`, `NotFound`,
  `Conflict`, `Unauthorized`, `Forbidden`, `Failure`) que a camada de Api
  usa para mapear para o status HTTP correto (`ProblemDetails`, RFC 7807).
- Exceptions continuam reservadas para falhas realmente excepcionais:
  infraestrutura indisponível, bugs de programação (`Guard` — ver
  `BuildingBlocks.Domain.Guards`), violação de invariante que nunca deveria
  acontecer se o código estiver correto.

## Consequências

**Positivas:**
- Fluxo de controle explícito: o tipo de retorno (`Result<T>`) já avisa o
  chamador que a operação pode falhar, sem precisar ler a implementação.
- Testes de domínio ficam diretos (`result.IsFailure.Should().BeTrue();
  result.Error.Code.Should().Be(...)`) sem `Assert.Throws`.
- Sem custo de captura de stack trace para erros esperados e frequentes.

**Negativas / trade-offs:**
- Desenvolvedores precisam lembrar de checar `IsSuccess`/`IsFailure` antes
  de acessar `.Value` (que lança se checado incorretamente) — mitigado por
  convenção de código e testes.
- Compor múltiplas operações que podem falhar exige encadear `Result`
  explicitamente (sem `try/catch` automático) — aceito como o preço de
  tornar erros visíveis no tipo.

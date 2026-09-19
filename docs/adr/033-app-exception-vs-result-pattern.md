# ADR-033: AppException — exceções personalizadas para erros excepcionais (não para erros de negócio)

**Status:** Aceito

## Contexto

O Result Pattern (ADR-007) é a regra para todo erro de negócio **esperado**
(usuário não encontrado, e-mail duplicado, papel inválido, ...) — nunca
lançar exceção nesses casos. Mas existem falhas genuinamente
**excepcionais**: um bug interno, uma dependência de infraestrutura fora
do ar de um jeito que não foi antecipado, uma violação de invariante que
"nunca deveria acontecer" se o resto do código estiver correto. Forçar
esses casos também a passar por `Result` obrigaria toda a cadeia de
chamadas entre o ponto da falha e o handler a propagar um `Result` de
falha manualmente, mesmo quando a falha é tão inesperada que não há
tratamento de negócio sensato para ela — só logar e devolver um erro
genérico ao cliente.

## Decisão

Introduzir `AppException` (`BuildingBlocks.Domain/Errors/AppException.cs`)
para esse segundo caso — e só para ele:

```csharp
public class AppException : Exception
{
    public ErrorCode Code { get; }
    public ErrorGroup Group { get; }
    public int HttpStatus { get; }

    public AppException(ErrorCode code, string? customMessage = null, Exception? inner = null);
}
```

Com quatro atalhos (`BuildingBlocks.Domain/Errors/Exceptions/`) para os
casos mais comuns de erro excepcional:

- `NotFoundException` → `ErrorCode.COMMON_NOT_FOUND`
- `ConflictException` → `ErrorCode.COMMON_ALREADY_EXISTS`
- `ForbiddenException` → `ErrorCode.AUTHORIZATION_FORBIDDEN`
- `IntegrationException(ErrorCode, ...)` → qualquer código do grupo
  `Integration` (o único atalho que recebe o código explicitamente, já
  que "qual falha de integração" varia por chamada)

`ExceptionHandlingMiddleware` (`Platform.Api`) captura `AppException`
antes do `catch (Exception)` genérico e converte para `ProblemDetails`
com os mesmos metadados que um `Result.Failure` teria — `Code`/`Group`/
`HttpStatus` já vêm prontos da própria exceção (via `ErrorCatalog`, no
construtor). Diferença chave em relação a uma exceção genuinamente não
tratada: a mensagem de uma `AppException` **é exposta** ao cliente (dev
ou produção), porque ela vem do próprio catálogo de erros ou de uma
mensagem customizada escrita por nós — nunca de detalhes internos de uma
falha de infraestrutura que poderiam vazar informação sensível.

### Regra prática de quando usar qual

| Situação | Mecanismo |
|---|---|
| Usuário não encontrado, e-mail duplicado, regra de negócio violada | `Result.Failure(ErrorFactory.From(ErrorCode.X))` |
| Bug interno / invariante que nunca deveria falhar | `AppException`/atalho, ou deixar a exceção original subir (vira `INTERNAL_UNEXPECTED` no middleware) |
| Falha de infraestrutura externa não recuperável no próprio handler | `IntegrationException(ErrorCode.INTEGRATION_X, ...)` |

Handlers de Platform **nesta etapa não lançam `AppException` em nenhum
caso real** — todos os fluxos de erro mapeados até aqui são de negócio
(Result Pattern). `AppException` existe como mecanismo pronto para uso
futuro (ex.: Fase 1+, quando integrações externas como CNJ/Anthropic
puderem ter falhas verdadeiramente excepcionais que não fazem sentido
modelar como um `Result` de negócio) e está coberto por testes unitários
diretos (`AppExceptionTests`) — não por um teste de integração ponta a
ponta, já que nenhum endpoint atual o aciona de verdade.

## Consequências

**Positivas:**
- Dois mecanismos claramente separados (Result para negócio, exceção
  para o excepcional) evitam a ambiguidade de "esse `NotFoundException`
  é uma regra de negócio disfarçada de exceção?" — a resposta é sempre
  não, por definição.
- `AppException` carrega os mesmos metadados do catálogo
  (`Code`/`Group`/`HttpStatus`) que um `Result.Failure`, então o cliente
  da API recebe um contrato de erro consistente independente de qual
  mecanismo gerou a falha internamente.

**Negativas / trade-offs:**
- Introduzir um segundo mecanismo de erro sempre carrega o risco de uso
  indevido (alguém lançar `AppException` para um erro de negócio comum,
  por ser "mais rápido" que definir um `Result.Failure`) — mitigado por
  revisão de código e pela regra explícita acima; não há enforcement
  automático disso (diferente da regra "nunca hardcodar string de erro",
  que tem um teste de arquitetura).
- Por não ter nenhum caso de uso real ainda, `AppException` corre o risco
  de "podridão silenciosa" (a API muda e ninguém percebe porque nada a
  exercita) até que a Fase 1+ realmente precise dela — mitigado pelos
  testes unitários diretos, que ao menos garantem que o mecanismo em si
  continua funcionando.

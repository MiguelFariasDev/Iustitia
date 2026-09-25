# ADR-040: ProblemDetails (RFC 7807) para erros

**Status:** Aceito — implementado nas Etapas 0.3/0.4, estendido na Etapa 0.7

## Contexto

Toda falha (validação, regra de negócio, exceção inesperada, rate limit,
conflito de idempotência) precisa de um formato de resposta único e
previsível para o cliente (React, iOS, integrações) — sem isso, cada
tipo de erro exigiria um tratamento especial no front. As Etapas 0.3/0.4 já
tinham `ResultExtensions.ToHttpResult` (para `Result`/`Result<T>`) e
`ExceptionHandlingMiddleware` (para `AppException`/exceções não tratadas)
gerando `ProblemDetails`, mas com extensions parciais (`errorType`,
`errorGroup`) — faltavam `errorCode`, `correlationId` e `timestamp`.

## Decisão

Todo erro (seja de `Result.Failure`, `AppException`, rate limit ou conflito
de idempotência) responde com o mesmo shape `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "TENANT_NOT_FOUND",
  "status": 404,
  "detail": "Escritório não encontrado.",
  "instance": "/api/v1/tenants/xxx",
  "errorType": "NotFound",
  "errorCode": "TENANT_NOT_FOUND",
  "errorGroup": "Tenant",
  "correlationId": "abc-123",
  "timestamp": "2026-09-23T10:00:00Z"
}
```

Três pontos de geração, cada um cobrindo uma origem de erro diferente, mas
todos produzindo o mesmo shape:

1. **`ResultExtensions.ToHttpResult`** (Platform.Api) — todo `Result`/
   `Result<T>` de falha retornado por um handler MediatR. `errorGroup` só é
   incluído quando `error.Code` corresponde a um `ErrorCode` do catálogo
   (erros ad-hoc de integrações externas, ex.: Supabase, ficam sem
   `errorGroup`, mas com `errorType`/`errorCode` sempre presentes).
2. **`ExceptionHandlingMiddleware`** (Platform.Api) — `AppException` (erro
   excepcional conhecido, ver ADR-033) e exceções genuinamente não
   tratadas. Em produção, o `detail` de uma exceção não-`AppException`
   nunca expõe `exception.Message`/stack trace — só uma mensagem genérica.
3. **`RateLimitingConfiguration`/`IdempotencyMiddleware`** (Platform.Api,
   Etapa 0.7) — 429 (rate limit) e 409 (conflito de idempotência) não
   passam por `Result`/`AppException` (acontecem antes do endpoint/handler
   rodar), então constroem o mesmo shape de `ProblemDetails` diretamente,
   lendo a mensagem/status do `ErrorCatalog` (`COMMON_RATE_LIMITED`,
   `COMMON_CONFLICT`) para nunca duplicar texto em dois lugares.

`correlationId` vem de `LogEnrichmentContext.CorrelationId` (ver ADR-037) —
o mesmo id que já aparece nos logs da requisição, permitindo ao suporte
correlacionar "o erro que o cliente reportou" com "a linha de log
correspondente" sem nenhuma pergunta adicional.

## Consequências

**Positivas:**
- Um único contrato de erro para todo o sistema — o frontend nunca precisa
  de um `if` especial para "é rate limit" vs. "é validação": sempre lê
  `status`+`errorCode`.
- Testado (Etapa 0.7): `ErrorHandlingTests` cobre `NotFound`/`Validation`/
  `Conflict` com as extensions completas; `RateLimitingTests`/
  `IdempotencyTests` cobrem 429/409 com o mesmo shape.
- Nenhuma mudança de contrato para os `ProblemDetails` já emitidos nas
  Etapas 0.3/0.4 — só adição de campos (`errorCode`, `correlationId`,
  `timestamp`), então nenhum teste/cliente existente quebrou.

**Negativas / trade-offs:**
- Três pontos de construção do mesmo shape (`ResultExtensions`,
  `ExceptionHandlingMiddleware`, `RateLimitingConfiguration`/
  `IdempotencyMiddleware`) em vez de uma única `ProblemDetailsFactory`
  centralizada — o prompt original desta etapa sugeria uma factory única;
  optamos por manter os pontos de origem já existentes (Result/Exception)
  e replicar o shape nos dois novos (rate limit/idempotência) para não
  arriscar quebrar o caminho já testado do Result Pattern. Uma extração
  para uma factory comum é um refactor de baixo risco para uma etapa
  futura, se a duplicação incomodar na prática.

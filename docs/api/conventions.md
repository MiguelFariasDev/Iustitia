# Convenções da API

Ver também: [ADR-039](../adr/039-api-versioning-url-segment.md),
[ADR-040](../adr/040-problemdetails-rfc7807.md),
[ADR-041](../adr/041-idempotency-key-posts-criticos.md).

## Versionamento

Toda rota usa `/api/v{versão}/<recurso>` (ex.: `/api/v1/tenants`). Versão
atual: **v1**. Toda resposta inclui o header `api-supported-versions` com
as versões disponíveis. Ver `VersionedEndpointExtensions.MapVersionedGroup`
para criar um grupo de endpoints versionado.

## Nomenclatura de endpoints

- Recursos no plural, kebab-case: `/tenants`, `/feature-flags`.
- Ações que não são CRUD puro viram um sub-recurso do verbo:
  `/tenants/{id}/suspend`, `/users/invite`, `/users/accept-invite`.
- IDs sempre como `{id:guid}` na rota (nunca inteiro sequencial — todo
  identificador do sistema é `Guid`).

## Autenticação e autorização

- `Authorization: Bearer <access_token>` (JWT emitido pelo Supabase Auth).
- Toda policy aplicada via `RequireAuthorization(Policies.X)` — ver
  `Platform.Infrastructure.Auth.Authorization.Policies`. Endpoints
  públicos usam `.AllowAnonymous()` explicitamente (nunca a ausência de
  qualquer chamada de autorização).

## Paginação

Listagens usam `page` (1-based, padrão 1) e `pageSize` (padrão 20) como
query params — ver `ListUsersRequest`/`ListAuditLogsRequest`. A resposta
não inclui metadados de paginação (total de páginas, etc.) nesta etapa;
revisitar quando um endpoint precisar disso.

## Filtros

Query params opcionais e nomeados pelo campo que filtram (`role`,
`specialty`, `isActive`, `entityType`, `userId`, ...) — nunca um único
parâmetro genérico `filter=`.

## Correlation ID

Toda requisição recebe (ou já enviou) um `X-Correlation-Id` — ver
ADR-037. Clientes podem enviar o próprio valor para correlacionar uma
chamada com um ID de rastreamento externo (ex.: um ID de sessão do
frontend); se omitido, o servidor gera um novo e o devolve no header de
resposta.

## Idempotency-Key

Obrigatório nos POSTs críticos (`/tenants`, `/users/invite`,
`/users/accept-invite` — ver ADR-041). Gerar um novo valor (ex.: `Guid`)
por operação lógica distinta — nunca reusar a mesma chave para dois
recursos diferentes.

## Erros (ProblemDetails)

Ver ADR-040 e `docs/errors/catalog.md`. Toda resposta de erro é
`application/problem+json` com `title` = código do catálogo, `status` =
HTTP status, `detail` = mensagem em pt-BR, e as extensions `errorType`,
`errorCode`, `errorGroup` (quando aplicável), `correlationId`, `timestamp`.

## Rate limiting

Ver ADR-039, ADR-067 e `RateLimitingConfiguration`. Limites por categoria:

| Rota | Limite | Chave |
|---|---|---|
| `/auth/login` | 5 / 5 min (janela deslizante) | IP |
| `/auth/forgot-password` | 3 / 1 h (janela deslizante) | IP |
| demais `/auth/*` | 10 / min | IP |
| `/webhooks/*` | 1000 / min | IP |
| escrita (POST/PUT/PATCH/DELETE) | 60 / min | usuário (`sub`; IP se anônimo) |
| leitura | 300 / min | usuário (`sub`; IP se anônimo) |

IPv6 é agrupado no /64. Estourar o limite responde 429 com `Retry-After` real e ProblemDetails
`COMMON_RATE_LIMITED`. Os contadores são em memória, por réplica (ver ADR-067).

## Ordem do pipeline de middlewares (Hosts/Api/Program.cs)

1. ForwardedHeaders (IP/esquema reais atrás do Front Door/ingress)
2. SecurityHeaders (headers OWASP em toda resposta)
3. CorrelationId
4. ExceptionHandling
5. RequestLogging
6. Authentication (nativo)
7. RateLimiting (`UseRateLimiter`) — depois da autenticação, para limitar por usuário
8. TenantContext
9. Authorization (nativo)
10. Idempotency
11. Endpoint (nativo)

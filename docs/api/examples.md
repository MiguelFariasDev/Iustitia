# Exemplos de request/response

Ver `docs/api/conventions.md` para as convenções gerais e
`docs/errors/catalog.md` para a lista completa de códigos de erro.

## Criar tenant (self-service)

```
POST /api/v1/tenants
Content-Type: application/json
Idempotency-Key: 6f1b6e6e-0c2e-4b8b-9b3a-2e7a6b9c9a11

{
  "name": "Escritório Modelo",
  "cnpj": "12.345.678/0001-99",
  "adminName": "Maria Advogada",
  "adminEmail": "maria@escritoriomodelo.com.br",
  "adminPassword": "SenhaForte123!"
}
```

Sucesso (201):
```
Location: /api/v1/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6

{
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "adminUserId": "9b2e1e2a-6b4a-4b8a-8b3a-2e7a6b9c9a22"
}
```

Erro — CNPJ duplicado (409):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "TENANT_CNPJ_DUPLICATED",
  "status": 409,
  "detail": "Já existe um escritório cadastrado com este CNPJ.",
  "instance": "/api/v1/tenants",
  "errorType": "Conflict",
  "errorCode": "TENANT_CNPJ_DUPLICATED",
  "errorGroup": "Tenant",
  "correlationId": "d3b1...",
  "timestamp": "2026-09-23T10:00:00Z"
}
```

## Login

```
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "maria@escritoriomodelo.com.br", "password": "SenhaForte123!" }
```

Erro — credenciais inválidas (401):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "AUTH_INVALID_CREDENTIALS",
  "status": 401,
  "detail": "E-mail ou senha inválidos.",
  "errorType": "Unauthorized",
  "errorCode": "AUTH_INVALID_CREDENTIALS",
  "errorGroup": "Auth",
  "correlationId": "...",
  "timestamp": "..."
}
```

## Convidar usuário (POST crítico — exige Idempotency-Key)

```
POST /api/v1/users/invite
Authorization: Bearer <token>
Idempotency-Key: 8c2e1e2a-6b4a-4b8a-8b3a-2e7a6b9c9a33
Content-Type: application/json

{ "email": "novo@escritoriomodelo.com.br", "name": "Novo Advogado", "role": "Lawyer", "specialty": "Cível" }
```

Erro — Idempotency-Key ausente (400):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "VALIDATION_REQUIRED_FIELD",
  "status": 400,
  "detail": "O campo 'Idempotency-Key' é obrigatório.",
  "errorType": "Validation",
  "errorCode": "VALIDATION_REQUIRED_FIELD",
  "errorGroup": "Validation",
  "correlationId": "...",
  "timestamp": "..."
}
```

Replay — mesma chave, mesmo corpo (200/201, com header extra):
```
Idempotency-Replayed: true
```
(corpo idêntico ao da primeira resposta)

Conflito — mesma chave, corpo diferente (409):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "COMMON_CONFLICT",
  "status": 409,
  "detail": "Conflito de idempotência: requisição duplicada com um payload diferente.",
  "errorType": "Conflict",
  "errorCode": "COMMON_CONFLICT",
  "errorGroup": "Common",
  "correlationId": "...",
  "timestamp": "..."
}
```

## Rate limit excedido (429)

```
HTTP/1.1 429 Too Many Requests
Retry-After: 60

{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "COMMON_RATE_LIMITED",
  "status": 429,
  "detail": "Muitas requisições. Tente novamente em instantes.",
  "errorCode": "COMMON_RATE_LIMITED",
  "errorGroup": "Common",
  "correlationId": "...",
  "timestamp": "..."
}
```

## Status/versão/usuário logado

```
GET /api/v1/health/live        -> 200, sem corpo relevante
GET /api/v1/health/ready       -> 200 (ou 503 se alguma dependência falhar)
GET /api/v1/version            -> { "version": "...", "environment": "Development", "buildDate": "..." }
GET /api/v1/me                 -> { "userId": "...", "email": "...", "tenantId": "...", "roles": ["Owner"] }
```

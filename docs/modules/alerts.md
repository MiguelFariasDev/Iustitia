# Alertas in-app

Ver ADR-065.

## Endpoints (`/api/v1/alerts`, policy `LawyerOrAbove`, sempre do usuário autenticado)

| Método | Rota | Descrição |
|---|---|---|
| GET | `/` | Lista paginada. Filtros: `type` (um ou vários, separados por vírgula), `isRead`, `from`, `to` |
| GET | `/summary` | `total`, `unread`, `unreadByType`, `totalByType` |
| GET | `/{id}` | Detalhe (404 também para alerta de outro usuário) |
| PATCH | `/{id}/read` | Marca como lido (409 `ALERT_ALREADY_READ`) |
| PATCH | `/read-all` | Marca todos como lidos; devolve a quantidade |
| DELETE | `/{id}` | Descarta (409 `ALERT_ALREADY_DISMISSED`) |

Erros: faixa `ALERT` 1400–1499 (`docs/errors/catalog.md`).

## Publicações (`/api/v1/publications`, `LawyerOrAbove`)

Novos/alterados nesta etapa: filtros `status` (vários), `clienteId`, `tribunal`, `dataInicio`,
`dataFim`, `busca` (dígitos do CNJ ou nome/documento do cliente), `ordem`
(`recent`/`oldest`/`tribunal`/`tribunal-desc`); `PATCH /{id}/read` (era POST);
`PATCH /{id}/status` (`{ status, notes }` — revisão humana, ADR-012);
`summary` com `pendingToday` e `treatedThisWeek`.

`Publication` ganhou `ClienteId`, `ResponsavelUserId` (vindos do processo na captura) e as
colunas derivadas `Tribunal`, `CnjDigits`, `PublishedOn`, que existem porque o EF não traduz
consultas dentro de value objects convertidos.

## Realtime

Canal privado `tenant:{tenantId}:alerts:{userId}`, evento `alert.created`, payload
`{ alertId, type, title, description, referenceId, createdAt }`. Configuração: `Supabase:Url` e
`Supabase:ServiceRoleKey` no backend; aplicar `infra/supabase/migrations/013_alerts.sql`.
Sem essas chaves o broadcast vira no-op.

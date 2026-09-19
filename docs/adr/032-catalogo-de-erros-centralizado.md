# ADR-032: Catálogo de Erros Centralizado

**Status:** Aceito

## Contexto

Até a Etapa 0.3, cada handler/Value Object construía seu próprio
`Error` com uma string de código ad-hoc (`Error.NotFound("tenant.not_found",
"Escritório não encontrado.")`). Isso funciona, mas tem três problemas
que crescem com o tamanho do sistema: (1) nenhuma garantia de que o
mesmo conceito de erro (ex.: "não encontrado") sempre usa o mesmo código
entre features diferentes; (2) nenhum lugar único para auditar "quais
erros este sistema pode retornar" (importante para documentação de API e
para quem constrói um frontend/integração contra ela); (3) mensagens e
status HTTP definidos individualmente em cada call site, sem uma fonte
de verdade.

## Decisão

Criar um **catálogo de erros centralizado** em
`BuildingBlocks.Domain/Errors/`:

- **`ErrorCode`** (enum): todo código de erro do sistema, com valor
  numérico dentro de uma faixa reservada por grupo (ver
  `docs/errors/groups.md` para a lista completa, incluindo faixas
  reservadas para fases futuras ainda não implementadas).
- **`ErrorGroup`** (enum): classificação de alto nível (Tenant, User,
  Auth, ...), usada para dashboards/relatórios e como campo de cada
  `ErrorDefinition`.
- **`ErrorDefinition`** (record): metadados fixos de um `ErrorCode` —
  grupo, código em texto, mensagem padrão em pt-BR (com placeholders
  `{0}`, `{1}`, ...), status HTTP e `ErrorType` (do Result Pattern, ver
  ADR-007).
- **`ErrorCatalog`** (estático): dicionário `ErrorCode → ErrorDefinition`
  — `Get`/`TryGet`/`All`. É a única fonte de verdade; um teste de
  arquitetura (`ErrorCatalogConventionTests.EveryErrorCode_
  HasACatalogDefinition`) garante que todo valor do enum `ErrorCode` tem
  uma entrada correspondente.
- **`ErrorFactory.From(ErrorCode, params object[] args)`**: único ponto
  para transformar um `ErrorCode` num `Error` do Result Pattern —
  handlers e Value Objects **nunca** constroem `Error.Validation(...)`/
  `Error.NotFound(...)`/etc. diretamente com uma string literal (ver
  `ErrorCatalogConventionTests`, que varre o código-fonte de
  `Platform.Application`/`Platform.Domain` procurando por essas chamadas
  proibidas).

`HttpStatus` de cada `ErrorDefinition` segue a mesma tabela de seis
valores já usada por `ResultExtensions.ToHttpResult` (400/401/403/404/
409/500) — nenhum valor HTTP fora dessa tabela foi introduzido nesta
etapa, mesmo para casos que intuitivamente pediriam um código mais
específico (ex.: 503/504 para timeouts de integração), para não
duplicar a lógica de mapeamento em dois lugares que poderiam divergir.
Ver `docs/errors/conventions.md` para como escolher grupo/faixa/status
ao adicionar um novo código.

### Faixas numéricas (Etapa 0.4)

| Grupo | Faixa |
|---|---|
| Common | 000–099 |
| Validation | 100–199 |
| Auth | 200–299 |
| Authorization | 300–399 |
| Tenant | 400–499 |
| User | 500–599 |
| Audit | 600–699 |
| Settings | 700–799 |
| FeatureFlags | 800–899 |
| Backup | 900–999 |
| Integration | 1000–1099 |
| Internal | 9000–9999 |

Faixas 1100–2499 estão reservadas (documentadas, sem nenhum `ErrorCode`
implementado ainda) para grupos de fases futuras — ver
`docs/errors/groups.md`.

### Escopo: onde o catálogo se aplica

O catálogo cobre erros de **regra de negócio da Application/Domain layer**
de Platform. Erros de infraestrutura externa com mensagem dinâmica —
`SupabaseAuthService` repassando o corpo de erro HTTP real do Supabase
Auth (`Error.Conflict("auth.conflict", messageDoSupabase)`) — continuam
usando `Error.X(...)` ad-hoc de propósito: a mensagem vem de uma API de
terceiros em tempo real, não é um texto fixo que caiba num molde do
catálogo, e forçar isso perderia informação útil do erro real. Nesse
caso, `ResultExtensions.ToProblem` simplesmente omite a extension
`errorGroup` do ProblemDetails (o `Enum.TryParse<ErrorCode>` para o
código ad-hoc falha, e a ausência da extension não quebra a resposta).

### AuditLog e ErrorCode

`audit_logs` não ganhou uma coluna própria para código de erro nesta
etapa (evitar uma migration só para isso); `IAuditService.RecordAsync`
ganhou um parâmetro opcional `ErrorCode? errorCode`, gravado dentro do
próprio `after` (`{"ErrorCode": "AUTH_ACCOUNT_DISABLED", "After": ...}`)
quando presente. Só há um caso de uso real hoje: `LoginHandler` audita
`AuditAction.LoginFailed` quando a tentativa de login é de uma conta
desativada — o único cenário de falha de login onde já se conhece o
usuário/tenant local antes de decidir a falha (login com credenciais
inválidas nunca chega a um `User` local, então não há tenant para
associar o registro — `AuditLog.TenantId` é obrigatório).

## Consequências

**Positivas:**
- Toda falha de negócio tem um código estável, documentado uma única vez
  (`docs/errors/catalog.md`) e reutilizável entre features.
- `ProblemDetails.Title` (código) e as extensions `errorType`/
  `errorGroup` dão ao cliente da API informação estruturada suficiente
  para tratar erros programaticamente, sem parsear a mensagem em
  linguagem natural.
- Adicionar um novo erro é um processo guiado (ver
  `docs/errors/how-to-add.md`), não uma decisão ad-hoc por handler.

**Negativas / trade-offs:**
- Mensagens do catálogo são necessariamente mais genéricas que uma
  mensagem escrita à mão para um call site específico (ex.:
  `TENANT_CANCELLED` cobre tanto "não pode suspender" quanto "não pode
  reativar" um tenant cancelado, com uma mensagem só). Aceito em troca de
  consistência — call sites que realmente precisarem de nuance podem usar
  os `args` de `ErrorFactory.From` para parametrizar a mensagem-molde.
- Alguns códigos existentes de Etapa 0.2/0.3 não tinham um equivalente
  exato no catálogo desta etapa (ex.: `"user.role.unchanged"`,
  `"tenant.not_suspended"`) — mapeados para `COMMON_INVALID_OPERATION`
  como fallback genérico de "operação inválida no estado atual". Se
  esses casos se tornarem frequentes o suficiente para merecer mensagem
  própria, a Etapa correspondente deve adicionar um `ErrorCode`
  específico (ver `docs/errors/how-to-add.md`).
- `Setting` e `FeatureFlag` foram promovidos de `Entity<TId>` para
  `AggregateRoot<TId>` nesta etapa (precisavam de `IRepository`
  completo — Add/Update — para os novos handlers de Settings/
  FeatureFlags, e só `AggregateRoot` satisfaz essa constraint genérica);
  isso não afeta o schema do banco, só a modelagem em C#.

# Catálogo de Erros

> Fonte de verdade programática: `src/BuildingBlocks/BuildingBlocks.Domain/Errors/ErrorCatalog.cs`.
> Este documento é a versão navegável da mesma informação — se divergirem,
> o código está certo e este arquivo desatualizado (corrija-o). Ver
> `conventions.md` para as regras de nomenclatura/faixas, e `how-to-add.md`
> para o processo de adicionar um código novo.

Legenda das colunas: **Código** (`ErrorCode`) · **Mensagem** (molde pt-BR,
`{0}`/`{1}` são placeholders preenchidos por `ErrorFactory.From`) ·
**HTTP** (status) · **Tipo** (`ErrorType`) · **Quando ocorre** · **Como resolver**
(orientação para quem consome a API).

## Common (000–099)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `COMMON_UNEXPECTED` | Ocorreu um erro inesperado. Tente novamente mais tarde. | 500 | Failure | Fallback genérico para um erro de negócio que não teve um código mais específico atribuído. | Tentar novamente; se persistir, reportar ao suporte. |
| `COMMON_NOT_FOUND` | Recurso não encontrado. | 404 | NotFound | Fallback genérico de "não encontrado" quando não há um código específico da entidade (a maioria dos casos usa o código específico, ex.: `TENANT_NOT_FOUND`). | Confirmar o identificador usado na requisição. |
| `COMMON_ALREADY_EXISTS` | Este recurso já existe. | 409 | Conflict | Fallback genérico de "já existe" quando não há um código específico. | Verificar se o recurso já foi criado antes de tentar de novo. |
| `COMMON_INVALID_OPERATION` | Esta operação não é permitida no estado atual. | 409 | Conflict | Fallback para transições de estado inválidas sem um código próprio (ex.: reativar um tenant que não está suspenso, mudar para o mesmo papel que o usuário já tem). | Consultar o estado atual do recurso antes de tentar a operação. |
| `COMMON_TIMEOUT` | A operação demorou mais que o esperado. Tente novamente. | 500 | Failure | Uma operação interna excedeu um tempo limite. | Tentar novamente; se persistir, reportar ao suporte. |
| `COMMON_UNAVAILABLE` | Serviço temporariamente indisponível. Tente novamente em instantes. | 500 | Failure | Uma dependência interna está temporariamente fora do ar. | Tentar novamente em instantes. |

## Validation (100–199)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `VALIDATION_REQUIRED_FIELD` | O campo '{0}' é obrigatório. | 400 | Validation | `NotEmpty`/`NotNull` do FluentValidation falhou (ver ADR-031). | Preencher o campo indicado. |
| `VALIDATION_INVALID_FORMAT` | O campo '{0}' está em um formato inválido. | 400 | Validation | `EmailAddress` ou uma regra customizada (`Must`) do FluentValidation falhou. | Corrigir o formato do campo indicado. |
| `VALIDATION_OUT_OF_RANGE` | O campo '{0}' está fora do intervalo permitido. | 400 | Validation | `GreaterThan`/`LessThan`/`GreaterThanOrEqual`/`LessThanOrEqual` falhou. | Ajustar o valor para dentro do intervalo permitido. |
| `VALIDATION_MAX_LENGTH_EXCEEDED` | O campo '{0}' excede o tamanho máximo permitido. | 400 | Validation | `MaximumLength` falhou. | Reduzir o tamanho do campo indicado. |
| `VALIDATION_MIN_LENGTH_NOT_MET` | O campo '{0}' não atinge o tamanho mínimo exigido. | 400 | Validation | `MinimumLength` falhou. | Aumentar o tamanho do campo indicado. |
| `VALIDATION_INVALID_ENUM` | O campo '{0}' contém um valor não reconhecido. | 400 | Validation | `IsInEnum` falhou. | Usar um dos valores aceitos para o campo indicado. |

## Auth (200–299)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `AUTH_INVALID_CREDENTIALS` | E-mail ou senha inválidos. | 401 | Unauthorized | Login com credenciais incorretas. | Conferir e-mail/senha; usar "esqueci minha senha" se necessário. |
| `AUTH_TOKEN_EXPIRED` | Sua sessão expirou. Faça login novamente. | 401 | Unauthorized | Access token expirado. | Renovar via `/api/v1/auth/refresh` ou fazer login novamente. |
| `AUTH_TOKEN_INVALID` | Token de acesso inválido. | 401 | Unauthorized | Access token malformado/assinatura inválida. | Fazer login novamente. |
| `AUTH_REFRESH_TOKEN_EXPIRED` | Sua sessão expirou. Faça login novamente. | 401 | Unauthorized | Refresh token expirado. | Fazer login novamente. |
| `AUTH_REFRESH_TOKEN_INVALID` | Refresh token inválido. | 401 | Unauthorized | Refresh token malformado/revogado. | Fazer login novamente. |
| `AUTH_EMAIL_NOT_CONFIRMED` | Confirme seu e-mail antes de continuar. | 403 | Forbidden | Reservado para quando a confirmação de e-mail se tornar obrigatória (não impõe isso ainda nesta etapa). | Confirmar o e-mail pelo link enviado no cadastro. |
| `AUTH_MFA_REQUIRED` | É necessário confirmar a autenticação em duas etapas. | 401 | Unauthorized | Reservado para quando MFA obrigatório for implementado (ver `docs/compliance/security.md`, pendência). | Completar o desafio de MFA. |
| `AUTH_ACCOUNT_LOCKED` | Conta bloqueada por excesso de tentativas. Tente novamente mais tarde. | 403 | Forbidden | Reservado para quando rate limiting de login for implementado (ver `docs/compliance/security.md`, pendência). | Aguardar o tempo de bloqueio ou contatar o suporte. |
| `AUTH_ACCOUNT_DISABLED` | Usuário desativado. Contate o administrador do escritório. | 403 | Forbidden | Login de um usuário com `IsActive = false` (ver `LoginHandler`). | Pedir a um Owner/Partner para reativar a conta. |

## Authorization (300–399)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `AUTHORIZATION_FORBIDDEN` | Você não tem permissão para executar esta ação. | 403 | Forbidden | Fallback genérico de autorização negada. | Verificar se o papel do usuário permite a ação. |
| `AUTHORIZATION_INSUFFICIENT_ROLE` | Seu papel no escritório não permite executar esta ação. | 403 | Forbidden | Reservado para negação de policy mais granular que o `[Authorize(Roles=...)]` padrão cobre hoje. | Pedir a um Owner/Partner para executar a ação, ou solicitar mudança de papel. |
| `AUTHORIZATION_TENANT_MISMATCH` | Requisição sem escritório identificado. | 401 | Unauthorized | `ICurrentUser.TenantId` nulo numa rota que exige tenant (JWT sem a claim `tenant_id` — ver ADR-029). | Fazer login novamente; se persistir, contatar o suporte (indica falha no Custom Access Token Hook). |
| `AUTHORIZATION_POLICY_VIOLATION` | Esta ação viola uma política de autorização do escritório. | 403 | Forbidden | Reservado para regras de autorização por recurso mais finas que "tem este papel" (ver ADR-028, Fase 1+). | Depende da política específica violada. |

## Tenant (400–499)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `TENANT_NOT_FOUND` | Escritório não encontrado. | 404 | NotFound | Id de tenant não existe, ou existe mas pertence a outro tenant (fail-closed contra IDOR — ver ADR-028). | Conferir o Id do escritório. |
| `TENANT_CNPJ_DUPLICATED` | Já existe um escritório cadastrado com este CNPJ. | 409 | Conflict | Reservado para quando a checagem de duplicidade de CNPJ (hoje o banco não impõe unicidade de CNPJ entre tenants) for implementada. | Usar outro CNPJ ou contatar o suporte se o cadastro já existe. |
| `TENANT_CNPJ_INVALID` | CNPJ inválido. | 400 | Validation | `CNPJ.Create` rejeitou o valor (vazio, tamanho errado, dígitos verificadores inválidos). | Conferir o CNPJ informado. |
| `TENANT_SUSPENDED` | O escritório está suspenso. | 409 | Conflict | Tentativa de suspender um tenant já suspenso. | Reativar o tenant antes, se a intenção era outra ação. |
| `TENANT_CANCELLED` | O escritório está cancelado. | 409 | Conflict | Tentativa de suspender/cancelar um tenant já cancelado. | Um tenant cancelado não pode ser reaberto por esta API. |
| `TENANT_NAME_REQUIRED` | O nome do escritório é obrigatório. | 400 | Validation | `Tenant.Create`/`Tenant.Update` recebeu nome vazio. | Informar um nome. |

## User (500–599)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `USER_NOT_FOUND` | Usuário não encontrado. | 404 | NotFound | Id de usuário não existe (ou não existe *neste tenant*, via filtro global — ver ADR-004); também retornado quando um login bem-sucedido no Supabase não tem registro local correspondente. | Conferir o Id do usuário. |
| `USER_EMAIL_DUPLICATED` | Já existe um usuário com este e-mail neste escritório. | 409 | Conflict | Convite para um e-mail já cadastrado no mesmo tenant. | Usar outro e-mail ou verificar o cadastro existente. |
| `USER_EMAIL_INVALID` | Formato de e-mail inválido. | 400 | Validation | `Email.Create` rejeitou o valor (vazio ou fora do formato esperado). | Conferir o e-mail informado. |
| `USER_OAB_INVALID` | Número da OAB inválido. | 400 | Validation | `OABNumber.Create` rejeitou o valor (número vazio, UF inválida). | Conferir número e UF da OAB. |
| `USER_ROLE_INVALID` | Papel de usuário inválido. | 400 | Validation | O papel informado não corresponde a nenhum `UserRole` (Owner/Partner/Lawyer/Intern/Admin/Financial). | Usar um dos papéis válidos. |
| `USER_ALREADY_ACTIVE` | O usuário já está ativo. | 409 | Conflict | `User.Activate()` chamado num usuário já ativo (inclusive aceite de convite duplicado). | Nenhuma ação necessária — o usuário já está ativo. |
| `USER_ALREADY_INACTIVE` | O usuário já está inativo. | 409 | Conflict | `User.Deactivate()` chamado num usuário já inativo. | Nenhuma ação necessária — o usuário já está inativo. |
| `USER_INVITE_EXPIRED` | Este convite expirou. Solicite um novo convite. | 409 | Conflict | Reservado para quando expiração de convite for implementada (hoje convites não expiram). | Solicitar um novo convite a um Partner/Owner. |
| `USER_INVITE_INVALID` | Convite inválido. | 401 | Unauthorized | Token de convite/acesso não corresponde a nenhum usuário conhecido (ver `AcceptInviteHandler`). | Verificar o link de convite; solicitar um novo se necessário. |
| `USER_CANNOT_DEACTIVATE_SELF` | Você não pode desativar a própria conta. | 409 | Conflict | Usuário autenticado tentando desativar a própria conta via `DeactivateUser`. | Pedir a outro Partner/Owner para desativar a conta. |
| `USER_CANNOT_REMOVE_LAST_OWNER` | O escritório precisa ter pelo menos um Owner ativo. | 409 | Conflict | Tentativa de desativar o único Owner ativo restante do tenant. | Promover outro usuário a Owner antes de desativar este. |

## Audit (600–699)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `AUDIT_LOG_NOT_FOUND` | Registro de auditoria não encontrado. | 404 | NotFound | Id de audit log não existe (ou não pertence ao tenant corrente). | Conferir o Id do registro. |
| `AUDIT_LOG_IMMUTABLE` | Registros de auditoria não podem ser alterados ou removidos. | 409 | Conflict | Reservado — não há hoje nenhum endpoint de update/delete de audit log (a API só expõe leitura), este código existe para o dia em que uma tentativa de mutação for exposta por engano. | Registros de auditoria são somente leitura por design. |

## Settings (700–799)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `SETTINGS_KEY_NOT_FOUND` | Configuração não encontrada. | 404 | NotFound | Reservado — `UpdateSettings` faz upsert (nunca falha por "não encontrado"); ficaria para um futuro endpoint de leitura por chave específica. | Conferir a chave da configuração. |
| `SETTINGS_KEY_INVALID` | Chave de configuração inválida. | 400 | Validation | `SettingKey.Create` rejeitou o valor (vazio ou fora do formato `[a-z0-9_.]+`). | Usar apenas letras minúsculas, números, `_` e `.` na chave. |
| `SETTINGS_VALUE_INVALID` | Valor de configuração inválido. | 400 | Validation | Reservado para validações de valor específicas por chave (hoje só se valida "é um JSON válido", via `UpdateSettingsValidator`, sem um `ErrorCode` próprio — ver nota abaixo). | Conferir o valor enviado. |
| `VALIDATION_INVALID_FORMAT` *(via ValidationBehavior)* | — | 400 | Validation | `UpdateSettingsCommand.ValueJson` que não é um JSON válido é pego pelo `ValidationBehavior` antes do handler (regra `Must(BeValidJson)`), não pela regra de domínio — por isso usa o código genérico de validação de formato, não `SETTINGS_VALUE_INVALID`. | Enviar um JSON válido. |

## FeatureFlags (800–899)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `FEATURE_FLAG_NOT_FOUND` | Feature flag não encontrada. | 404 | NotFound | Reservado — `UpdateFeatureFlag` faz upsert (nunca falha por "não encontrado"); ficaria para um futuro endpoint de leitura por chave específica. | Conferir a chave da feature flag. |
| `FEATURE_FLAG_KEY_INVALID` | Chave de feature flag inválida. | 400 | Validation | `FeatureFlagKey.Create` rejeitou o valor (vazio ou fora do formato `[a-z0-9_.]+`). | Usar apenas letras minúsculas, números, `_` e `.` na chave. |
| `FEATURE_FLAG_KEY_DUPLICATED` | Já existe uma feature flag com esta chave. | 409 | Conflict | Reservado — `UpdateFeatureFlag` é idempotente por design (nunca tenta criar uma chave já existente); ficaria para um futuro endpoint de criação estrita (não upsert). | N/A — o upsert atual não produz este erro. |

## Backup (900–999)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `BACKUP_FAILED` | Falha ao executar o backup. | 500 | Failure | Reservado para o processo de backup (`.github/workflows/backup.yml`, ver `docs/compliance/backups.md`) reportar falhas via API no futuro — hoje o backup roda fora da API (GitHub Actions), sem um endpoint que retorne este código. | Verificar os logs do workflow de backup. |
| `BACKUP_ENCRYPTION_FAILED` | Falha ao criptografar o backup. | 500 | Failure | Reservado (ver acima). | Verificar os logs do workflow de backup. |
| `BACKUP_UPLOAD_FAILED` | Falha ao enviar o backup para o armazenamento externo. | 500 | Failure | Reservado (ver acima). | Verificar os logs do workflow de backup. |

## Integration (1000–1099)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `INTEGRATION_UNAVAILABLE` | Serviço externo '{0}' está indisponível no momento. | 500 | Failure | Reservado para uso com `IntegrationException` (ver ADR-033) quando integrações externas (CNJ, Anthropic, ...) forem implementadas — nenhum handler desta etapa o produz ainda. | Tentar novamente mais tarde. |
| `INTEGRATION_TIMEOUT` | Serviço externo '{0}' demorou mais que o esperado para responder. | 500 | Failure | Reservado (ver acima). | Tentar novamente mais tarde. |
| `INTEGRATION_AUTH_FAILED` | Falha de autenticação com o serviço externo '{0}'. | 401 | Unauthorized | Reservado (ver acima). | Reportar ao suporte — geralmente indica credencial expirada/inválida do lado do servidor. |
| `INTEGRATION_RATE_LIMITED` | Limite de requisições ao serviço externo '{0}' excedido. Tente novamente mais tarde. | 500 | Failure | Reservado (ver acima). | Tentar novamente mais tarde. |
| `INTEGRATION_INVALID_RESPONSE` | Resposta inesperada do serviço externo '{0}'. | 500 | Failure | Reservado (ver acima). | Reportar ao suporte. |

## Internal (9000–9999)

| Código | Mensagem | HTTP | Tipo | Quando ocorre | Como resolver |
|---|---|---|---|---|---|
| `INTERNAL_UNEXPECTED` | Ocorreu um erro interno inesperado. | 500 | Failure | Qualquer exceção não tratada capturada por `ExceptionHandlingMiddleware` (bug real, não erro de negócio). | Reportar ao suporte com o horário exato da requisição. |
| `INTERNAL_DATABASE_ERROR` | Erro ao acessar o banco de dados. | 500 | Failure | Reservado para uso com `AppException`/`IntegrationException`-like ao redor de falhas de acesso a dados que não sejam simplesmente repassadas como `INTERNAL_UNEXPECTED`. | Reportar ao suporte. |
| `INTERNAL_SERIALIZATION_ERROR` | Erro ao processar os dados da requisição. | 500 | Failure | Reservado (ver acima), para falhas de (de)serialização que não sejam capturadas como erro de validação. | Reportar ao suporte. |

## Nota sobre erros de infraestrutura externa (fora do catálogo)

`Platform.Infrastructure.Auth.SupabaseAuthService` repassa erros HTTP do
Supabase Auth com códigos ad-hoc (`"auth.invalid_credentials"`,
`"auth.conflict"`, `"auth.bad_request"`, `"auth.not_found"`,
`"auth.supabase_error"`) — esses códigos **não existem** neste catálogo
de propósito: a mensagem vem da API do Supabase em tempo real, não é um
texto fixo (ver ADR-032, seção "Escopo"). O `ProblemDetails` desses casos
tem `title` igual a esse código ad-hoc e **não** tem a extension
`errorGroup` (só `errorType`).

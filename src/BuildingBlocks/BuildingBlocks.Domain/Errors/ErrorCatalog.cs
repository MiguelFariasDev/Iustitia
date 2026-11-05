using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.BuildingBlocks.Domain.Errors;

/// <summary>
/// Fonte única de verdade para os metadados de todo <see cref="ErrorCode"/> do sistema —
/// ver docs/errors/catalog.md para a versão navegável desta mesma informação. HttpStatus
/// segue a mesma tabela usada por Platform.Api.Extensions.ResultExtensions.ToHttpResult
/// (Validation→400, NotFound→404, Conflict→409, Unauthorized→401, Forbidden→403,
/// Failure→500) — não introduzimos aqui nenhum status fora dessas seis opções para não
/// duplicar a lógica de mapeamento em dois lugares divergentes.
/// </summary>
public static class ErrorCatalog
{
    private static readonly IReadOnlyDictionary<ErrorCode, ErrorDefinition> Definitions =
        new Dictionary<ErrorCode, ErrorDefinition>
        {
            // COMMON
            [ErrorCode.COMMON_UNEXPECTED] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_UNEXPECTED),
                "Ocorreu um erro inesperado. Tente novamente mais tarde.", 500, ErrorType.Failure),
            [ErrorCode.COMMON_NOT_FOUND] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_NOT_FOUND),
                "Recurso não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.COMMON_ALREADY_EXISTS] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_ALREADY_EXISTS),
                "Este recurso já existe.", 409, ErrorType.Conflict),
            [ErrorCode.COMMON_INVALID_OPERATION] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_INVALID_OPERATION),
                "Esta operação não é permitida no estado atual.", 409, ErrorType.Conflict),
            [ErrorCode.COMMON_TIMEOUT] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_TIMEOUT),
                "A operação demorou mais que o esperado. Tente novamente.", 500, ErrorType.Failure),
            [ErrorCode.COMMON_UNAVAILABLE] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_UNAVAILABLE),
                "Serviço temporariamente indisponível. Tente novamente em instantes.", 500, ErrorType.Failure),
            // HttpStatus 429 aqui é só metadado de referência: rate limiting é aplicado pelo
            // middleware nativo do ASP.NET Core (ver RateLimitingConfiguration), antes de
            // qualquer Result/handler rodar — nunca passa por ResultExtensions.ToHttpResult.
            [ErrorCode.COMMON_RATE_LIMITED] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_RATE_LIMITED),
                "Muitas requisições. Tente novamente em instantes.", 429, ErrorType.Failure),
            [ErrorCode.COMMON_CONFLICT] = new(ErrorGroup.Common, nameof(ErrorCode.COMMON_CONFLICT),
                "Conflito de idempotência: requisição duplicada com um payload diferente.", 409, ErrorType.Conflict),

            // VALIDATION
            [ErrorCode.VALIDATION_REQUIRED_FIELD] = new(ErrorGroup.Validation, nameof(ErrorCode.VALIDATION_REQUIRED_FIELD),
                "O campo '{0}' é obrigatório.", 400, ErrorType.Validation),
            [ErrorCode.VALIDATION_INVALID_FORMAT] = new(ErrorGroup.Validation, nameof(ErrorCode.VALIDATION_INVALID_FORMAT),
                "O campo '{0}' está em um formato inválido.", 400, ErrorType.Validation),
            [ErrorCode.VALIDATION_OUT_OF_RANGE] = new(ErrorGroup.Validation, nameof(ErrorCode.VALIDATION_OUT_OF_RANGE),
                "O campo '{0}' está fora do intervalo permitido.", 400, ErrorType.Validation),
            [ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED] = new(ErrorGroup.Validation, nameof(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED),
                "O campo '{0}' excede o tamanho máximo permitido.", 400, ErrorType.Validation),
            [ErrorCode.VALIDATION_MIN_LENGTH_NOT_MET] = new(ErrorGroup.Validation, nameof(ErrorCode.VALIDATION_MIN_LENGTH_NOT_MET),
                "O campo '{0}' não atinge o tamanho mínimo exigido.", 400, ErrorType.Validation),
            [ErrorCode.VALIDATION_INVALID_ENUM] = new(ErrorGroup.Validation, nameof(ErrorCode.VALIDATION_INVALID_ENUM),
                "O campo '{0}' contém um valor não reconhecido.", 400, ErrorType.Validation),

            // AUTH
            [ErrorCode.AUTH_INVALID_CREDENTIALS] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_INVALID_CREDENTIALS),
                "E-mail ou senha inválidos.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTH_TOKEN_EXPIRED] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_TOKEN_EXPIRED),
                "Sua sessão expirou. Faça login novamente.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTH_TOKEN_INVALID] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_TOKEN_INVALID),
                "Token de acesso inválido.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTH_REFRESH_TOKEN_EXPIRED] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_REFRESH_TOKEN_EXPIRED),
                "Sua sessão expirou. Faça login novamente.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTH_REFRESH_TOKEN_INVALID] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_REFRESH_TOKEN_INVALID),
                "Refresh token inválido.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTH_EMAIL_NOT_CONFIRMED] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_EMAIL_NOT_CONFIRMED),
                "Confirme seu e-mail antes de continuar.", 403, ErrorType.Forbidden),
            [ErrorCode.AUTH_MFA_REQUIRED] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_MFA_REQUIRED),
                "É necessário confirmar a autenticação em duas etapas.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTH_ACCOUNT_LOCKED] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_ACCOUNT_LOCKED),
                "Conta bloqueada por excesso de tentativas. Tente novamente mais tarde.", 403, ErrorType.Forbidden),
            [ErrorCode.AUTH_ACCOUNT_DISABLED] = new(ErrorGroup.Auth, nameof(ErrorCode.AUTH_ACCOUNT_DISABLED),
                "Usuário desativado. Contate o administrador do escritório.", 403, ErrorType.Forbidden),

            // AUTHORIZATION
            [ErrorCode.AUTHORIZATION_FORBIDDEN] = new(ErrorGroup.Authorization, nameof(ErrorCode.AUTHORIZATION_FORBIDDEN),
                "Você não tem permissão para executar esta ação.", 403, ErrorType.Forbidden),
            [ErrorCode.AUTHORIZATION_INSUFFICIENT_ROLE] = new(ErrorGroup.Authorization, nameof(ErrorCode.AUTHORIZATION_INSUFFICIENT_ROLE),
                "Seu papel no escritório não permite executar esta ação.", 403, ErrorType.Forbidden),
            [ErrorCode.AUTHORIZATION_TENANT_MISMATCH] = new(ErrorGroup.Authorization, nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH),
                "Requisição sem escritório identificado.", 401, ErrorType.Unauthorized),
            [ErrorCode.AUTHORIZATION_POLICY_VIOLATION] = new(ErrorGroup.Authorization, nameof(ErrorCode.AUTHORIZATION_POLICY_VIOLATION),
                "Esta ação viola uma política de autorização do escritório.", 403, ErrorType.Forbidden),

            // TENANT
            [ErrorCode.TENANT_NOT_FOUND] = new(ErrorGroup.Tenant, nameof(ErrorCode.TENANT_NOT_FOUND),
                "Escritório não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.TENANT_CNPJ_DUPLICATED] = new(ErrorGroup.Tenant, nameof(ErrorCode.TENANT_CNPJ_DUPLICATED),
                "Já existe um escritório cadastrado com este CNPJ.", 409, ErrorType.Conflict),
            [ErrorCode.TENANT_CNPJ_INVALID] = new(ErrorGroup.Tenant, nameof(ErrorCode.TENANT_CNPJ_INVALID),
                "CNPJ inválido.", 400, ErrorType.Validation),
            [ErrorCode.TENANT_SUSPENDED] = new(ErrorGroup.Tenant, nameof(ErrorCode.TENANT_SUSPENDED),
                "O escritório está suspenso.", 409, ErrorType.Conflict),
            [ErrorCode.TENANT_CANCELLED] = new(ErrorGroup.Tenant, nameof(ErrorCode.TENANT_CANCELLED),
                "O escritório está cancelado.", 409, ErrorType.Conflict),
            [ErrorCode.TENANT_NAME_REQUIRED] = new(ErrorGroup.Tenant, nameof(ErrorCode.TENANT_NAME_REQUIRED),
                "O nome do escritório é obrigatório.", 400, ErrorType.Validation),

            // USER
            [ErrorCode.USER_NOT_FOUND] = new(ErrorGroup.User, nameof(ErrorCode.USER_NOT_FOUND),
                "Usuário não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.USER_EMAIL_DUPLICATED] = new(ErrorGroup.User, nameof(ErrorCode.USER_EMAIL_DUPLICATED),
                "Já existe um usuário com este e-mail neste escritório.", 409, ErrorType.Conflict),
            [ErrorCode.USER_EMAIL_INVALID] = new(ErrorGroup.User, nameof(ErrorCode.USER_EMAIL_INVALID),
                "Formato de e-mail inválido.", 400, ErrorType.Validation),
            [ErrorCode.USER_OAB_INVALID] = new(ErrorGroup.User, nameof(ErrorCode.USER_OAB_INVALID),
                "Número da OAB inválido.", 400, ErrorType.Validation),
            [ErrorCode.USER_ROLE_INVALID] = new(ErrorGroup.User, nameof(ErrorCode.USER_ROLE_INVALID),
                "Papel de usuário inválido.", 400, ErrorType.Validation),
            [ErrorCode.USER_ALREADY_ACTIVE] = new(ErrorGroup.User, nameof(ErrorCode.USER_ALREADY_ACTIVE),
                "O usuário já está ativo.", 409, ErrorType.Conflict),
            [ErrorCode.USER_ALREADY_INACTIVE] = new(ErrorGroup.User, nameof(ErrorCode.USER_ALREADY_INACTIVE),
                "O usuário já está inativo.", 409, ErrorType.Conflict),
            [ErrorCode.USER_INVITE_EXPIRED] = new(ErrorGroup.User, nameof(ErrorCode.USER_INVITE_EXPIRED),
                "Este convite expirou. Solicite um novo convite.", 409, ErrorType.Conflict),
            [ErrorCode.USER_INVITE_INVALID] = new(ErrorGroup.User, nameof(ErrorCode.USER_INVITE_INVALID),
                "Convite inválido.", 401, ErrorType.Unauthorized),
            [ErrorCode.USER_CANNOT_DEACTIVATE_SELF] = new(ErrorGroup.User, nameof(ErrorCode.USER_CANNOT_DEACTIVATE_SELF),
                "Você não pode desativar a própria conta.", 409, ErrorType.Conflict),
            [ErrorCode.USER_CANNOT_REMOVE_LAST_OWNER] = new(ErrorGroup.User, nameof(ErrorCode.USER_CANNOT_REMOVE_LAST_OWNER),
                "O escritório precisa ter pelo menos um Owner ativo.", 409, ErrorType.Conflict),

            // AUDIT
            [ErrorCode.AUDIT_LOG_NOT_FOUND] = new(ErrorGroup.Audit, nameof(ErrorCode.AUDIT_LOG_NOT_FOUND),
                "Registro de auditoria não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.AUDIT_LOG_IMMUTABLE] = new(ErrorGroup.Audit, nameof(ErrorCode.AUDIT_LOG_IMMUTABLE),
                "Registros de auditoria não podem ser alterados ou removidos.", 409, ErrorType.Conflict),

            // SETTINGS
            [ErrorCode.SETTINGS_KEY_NOT_FOUND] = new(ErrorGroup.Settings, nameof(ErrorCode.SETTINGS_KEY_NOT_FOUND),
                "Configuração não encontrada.", 404, ErrorType.NotFound),
            [ErrorCode.SETTINGS_KEY_INVALID] = new(ErrorGroup.Settings, nameof(ErrorCode.SETTINGS_KEY_INVALID),
                "Chave de configuração inválida.", 400, ErrorType.Validation),
            [ErrorCode.SETTINGS_VALUE_INVALID] = new(ErrorGroup.Settings, nameof(ErrorCode.SETTINGS_VALUE_INVALID),
                "Valor de configuração inválido.", 400, ErrorType.Validation),

            // FEATURE_FLAGS
            [ErrorCode.FEATURE_FLAG_NOT_FOUND] = new(ErrorGroup.FeatureFlags, nameof(ErrorCode.FEATURE_FLAG_NOT_FOUND),
                "Feature flag não encontrada.", 404, ErrorType.NotFound),
            [ErrorCode.FEATURE_FLAG_KEY_INVALID] = new(ErrorGroup.FeatureFlags, nameof(ErrorCode.FEATURE_FLAG_KEY_INVALID),
                "Chave de feature flag inválida.", 400, ErrorType.Validation),
            [ErrorCode.FEATURE_FLAG_KEY_DUPLICATED] = new(ErrorGroup.FeatureFlags, nameof(ErrorCode.FEATURE_FLAG_KEY_DUPLICATED),
                "Já existe uma feature flag com esta chave.", 409, ErrorType.Conflict),

            // BACKUP
            [ErrorCode.BACKUP_FAILED] = new(ErrorGroup.Backup, nameof(ErrorCode.BACKUP_FAILED),
                "Falha ao executar o backup.", 500, ErrorType.Failure),
            [ErrorCode.BACKUP_ENCRYPTION_FAILED] = new(ErrorGroup.Backup, nameof(ErrorCode.BACKUP_ENCRYPTION_FAILED),
                "Falha ao criptografar o backup.", 500, ErrorType.Failure),
            [ErrorCode.BACKUP_UPLOAD_FAILED] = new(ErrorGroup.Backup, nameof(ErrorCode.BACKUP_UPLOAD_FAILED),
                "Falha ao enviar o backup para o armazenamento externo.", 500, ErrorType.Failure),

            // INTEGRATION
            [ErrorCode.INTEGRATION_UNAVAILABLE] = new(ErrorGroup.Integration, nameof(ErrorCode.INTEGRATION_UNAVAILABLE),
                "Serviço externo '{0}' está indisponível no momento.", 500, ErrorType.Failure),
            [ErrorCode.INTEGRATION_TIMEOUT] = new(ErrorGroup.Integration, nameof(ErrorCode.INTEGRATION_TIMEOUT),
                "Serviço externo '{0}' demorou mais que o esperado para responder.", 500, ErrorType.Failure),
            [ErrorCode.INTEGRATION_AUTH_FAILED] = new(ErrorGroup.Integration, nameof(ErrorCode.INTEGRATION_AUTH_FAILED),
                "Falha de autenticação com o serviço externo '{0}'.", 401, ErrorType.Unauthorized),
            [ErrorCode.INTEGRATION_RATE_LIMITED] = new(ErrorGroup.Integration, nameof(ErrorCode.INTEGRATION_RATE_LIMITED),
                "Limite de requisições ao serviço externo '{0}' excedido. Tente novamente mais tarde.", 500, ErrorType.Failure),
            [ErrorCode.INTEGRATION_INVALID_RESPONSE] = new(ErrorGroup.Integration, nameof(ErrorCode.INTEGRATION_INVALID_RESPONSE),
                "Resposta inesperada do serviço externo '{0}'.", 500, ErrorType.Failure),

            // CLIENT (módulo Legal)
            [ErrorCode.CLIENT_NOT_FOUND] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_NOT_FOUND),
                "Cliente não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.CLIENT_DOCUMENT_DUPLICATED] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_DOCUMENT_DUPLICATED),
                "Já existe um cliente cadastrado com este CPF/CNPJ.", 409, ErrorType.Conflict),
            [ErrorCode.CLIENT_DOCUMENT_INVALID] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_DOCUMENT_INVALID),
                "CPF ou CNPJ inválido.", 400, ErrorType.Validation),
            [ErrorCode.CLIENT_NAME_REQUIRED] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_NAME_REQUIRED),
                "O nome do cliente é obrigatório.", 400, ErrorType.Validation),
            [ErrorCode.CLIENT_ALREADY_ACTIVE] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_ALREADY_ACTIVE),
                "Este cliente já está ativo.", 409, ErrorType.Conflict),
            [ErrorCode.CLIENT_ALREADY_INACTIVE] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_ALREADY_INACTIVE),
                "Este cliente já está inativo.", 409, ErrorType.Conflict),
            [ErrorCode.CLIENT_EMAIL_INVALID] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_EMAIL_INVALID),
                "E-mail do cliente inválido.", 400, ErrorType.Validation),
            [ErrorCode.CLIENT_PHONE_INVALID] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_PHONE_INVALID),
                "Telefone inválido. Informe DDD e número (10 ou 11 dígitos).", 400, ErrorType.Validation),
            [ErrorCode.CLIENT_BIRTHDATE_INVALID] = new(ErrorGroup.Client, nameof(ErrorCode.CLIENT_BIRTHDATE_INVALID),
                "Data de nascimento inválida: não pode estar no futuro.", 400, ErrorType.Validation),

            // PROCESS (módulo Legal)
            [ErrorCode.PROCESS_NOT_FOUND] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_NOT_FOUND),
                "Processo não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.PROCESS_CNJ_DUPLICATED] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_CNJ_DUPLICATED),
                "Já existe um processo cadastrado com este número CNJ.", 409, ErrorType.Conflict),
            [ErrorCode.PROCESS_CNJ_INVALID] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_CNJ_INVALID),
                "Número de processo CNJ inválido.", 400, ErrorType.Validation),
            [ErrorCode.PROCESS_CLIENT_REQUIRED] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_CLIENT_REQUIRED),
                "O processo precisa estar vinculado a um cliente.", 400, ErrorType.Validation),
            [ErrorCode.PROCESS_RESPONSIBLE_REQUIRED] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_RESPONSIBLE_REQUIRED),
                "O processo precisa ter um advogado responsável.", 400, ErrorType.Validation),
            [ErrorCode.PROCESS_ALREADY_ARCHIVED] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_ALREADY_ARCHIVED),
                "Este processo já está arquivado.", 409, ErrorType.Conflict),
            [ErrorCode.PROCESS_INVALID_STATUS_TRANSITION] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_INVALID_STATUS_TRANSITION),
                "Não é possível mudar o processo de '{0}' para '{1}'.", 409, ErrorType.Conflict),
            [ErrorCode.PROCESS_CLIENT_NOT_FOUND] = new(ErrorGroup.Process, nameof(ErrorCode.PROCESS_CLIENT_NOT_FOUND),
                "O cliente informado para o processo não existe.", 404, ErrorType.NotFound),

            // PUBLICATION (módulo Legal — ver docs/modules/legal/publications.md)
            [ErrorCode.PUBLICATION_NOT_FOUND] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_NOT_FOUND),
                "Publicação não encontrada.", 404, ErrorType.NotFound),
            [ErrorCode.PUBLICATION_ALREADY_EXISTS] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_ALREADY_EXISTS),
                "Esta publicação já foi capturada.", 409, ErrorType.Conflict),
            [ErrorCode.PUBLICATION_INVALID_CNJ] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_INVALID_CNJ),
                "Número de processo CNJ inválido.", 400, ErrorType.Validation),
            [ErrorCode.PUBLICATION_INVALID_DATE] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_INVALID_DATE),
                "Data de publicação inválida: não pode estar no futuro.", 400, ErrorType.Validation),
            [ErrorCode.PUBLICATION_EMPTY_CONTENT] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_EMPTY_CONTENT),
                "O conteúdo da publicação não pode estar vazio.", 400, ErrorType.Validation),
            // Conflict (não Validation): a captura é idempotente por ExternalId (ver ADR-056) —
            // o handler trata a duplicata como sucesso com Existing=true e este código só
            // aparece quando alguém tenta criar a mesma publicação por outra via.
            [ErrorCode.PUBLICATION_DUPLICATED_EXTERNAL_ID] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_DUPLICATED_EXTERNAL_ID),
                "Já existe uma publicação com este identificador do CNJ.", 409, ErrorType.Conflict),
            [ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION),
                "Não é possível mudar a publicação de '{0}' para '{1}'.", 409, ErrorType.Conflict),
            [ErrorCode.PUBLICATION_ALREADY_READ] = new(ErrorGroup.Publication, nameof(ErrorCode.PUBLICATION_ALREADY_READ),
                "Esta publicação já foi marcada como lida.", 409, ErrorType.Conflict),

            // ALERT (alertas in-app — ver docs/modules/alerts.md)
            [ErrorCode.ALERT_NOT_FOUND] = new(ErrorGroup.Alert, nameof(ErrorCode.ALERT_NOT_FOUND),
                "Alerta não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.ALERT_ALREADY_READ] = new(ErrorGroup.Alert, nameof(ErrorCode.ALERT_ALREADY_READ),
                "Este alerta já foi marcado como lido.", 409, ErrorType.Conflict),
            [ErrorCode.ALERT_ALREADY_DISMISSED] = new(ErrorGroup.Alert, nameof(ErrorCode.ALERT_ALREADY_DISMISSED),
                "Este alerta já foi descartado.", 409, ErrorType.Conflict),

            // ATENDIMENTO (módulo Atendimento — ver docs/modules/atendimento.md)
            [ErrorCode.ATENDIMENTO_NOT_FOUND] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_NOT_FOUND),
                "Atendimento não encontrado.", 404, ErrorType.NotFound),
            [ErrorCode.ATENDIMENTO_PHONE_INVALID] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_PHONE_INVALID),
                "Telefone inválido. Informe o número com DDI e DDD (10 a 15 dígitos).", 400, ErrorType.Validation),
            [ErrorCode.ATENDIMENTO_CLOSED] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_CLOSED),
                "Este atendimento está encerrado.", 409, ErrorType.Conflict),
            [ErrorCode.ATENDIMENTO_INVALID_STATUS_TRANSITION] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_INVALID_STATUS_TRANSITION),
                "Não é possível mudar o atendimento de '{0}' para '{1}'.", 409, ErrorType.Conflict),
            [ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_ALREADY_ASSIGNED),
                "Este atendimento já está sendo conduzido por outro advogado.", 409, ErrorType.Conflict),
            [ErrorCode.ATENDIMENTO_MESSAGE_EMPTY] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_MESSAGE_EMPTY),
                "A mensagem não pode ser vazia.", 400, ErrorType.Validation),
            [ErrorCode.ATENDIMENTO_MESSAGE_TOO_LONG] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_MESSAGE_TOO_LONG),
                "A mensagem excede o limite de {0} caracteres do WhatsApp.", 400, ErrorType.Validation),
            [ErrorCode.ATENDIMENTO_MESSAGE_INVALID_SENDER] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_MESSAGE_INVALID_SENDER),
                "Combinação inválida de direção e remetente da mensagem.", 400, ErrorType.Validation),
            [ErrorCode.ATENDIMENTO_BOT_BLOCKED] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_BOT_BLOCKED),
                "O bot não pode responder: o atendimento está com um humano.", 409, ErrorType.Conflict),
            [ErrorCode.ATENDIMENTO_DELIVERY_FAILED] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_DELIVERY_FAILED),
                "A mensagem foi registrada, mas não foi possível enviá-la ao WhatsApp. Tente novamente.", 500, ErrorType.Failure),
            [ErrorCode.ATENDIMENTO_BOT_API_KEY_NOT_FOUND] = new(ErrorGroup.Atendimento, nameof(ErrorCode.ATENDIMENTO_BOT_API_KEY_NOT_FOUND),
                "Chave de API do bot não encontrada.", 404, ErrorType.NotFound),

            // CNJ (integração com o DJEN — ver docs/modules/legal/cnj-capture.md)
            [ErrorCode.CNJ_UNAVAILABLE] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_UNAVAILABLE),
                "O serviço de publicações do CNJ está indisponível no momento.", 500, ErrorType.Failure),
            [ErrorCode.CNJ_TIMEOUT] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_TIMEOUT),
                "O serviço de publicações do CNJ demorou mais que o esperado para responder.", 500, ErrorType.Failure),
            [ErrorCode.CNJ_RATE_LIMITED] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_RATE_LIMITED),
                "Limite de consultas ao CNJ excedido. A captura será retomada automaticamente.", 500, ErrorType.Failure),
            [ErrorCode.CNJ_INVALID_RESPONSE] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_INVALID_RESPONSE),
                "O CNJ devolveu uma resposta em formato inesperado.", 500, ErrorType.Failure),
            [ErrorCode.CNJ_AUTH_FAILED] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_AUTH_FAILED),
                "Falha de autenticação ao consultar o CNJ.", 401, ErrorType.Unauthorized),
            [ErrorCode.CNJ_INVALID_REQUEST] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_INVALID_REQUEST),
                "A consulta enviada ao CNJ foi rejeitada por parâmetros inválidos.", 400, ErrorType.Validation),
            // 404 e não 500: o processo não estar no DataJud é desfecho normal — nem todo
            // tribunal envia todos os processos, e o cadastro manual segue disponível.
            [ErrorCode.CNJ_NOT_FOUND] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_NOT_FOUND),
                "Processo não encontrado no DataJud. Preencha os dados manualmente.", 404, ErrorType.NotFound),
            [ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED] = new(ErrorGroup.Cnj, nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED),
                "O tribunal '{0}' não é atendido pela consulta pública do DataJud.", 400, ErrorType.Validation),

            // AI (Anthropic — ver ADR-067 e docs/integrations/anthropic.md)
            [ErrorCode.AI_INVALID_REQUEST] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_INVALID_REQUEST),
                "A requisição enviada ao serviço de IA foi rejeitada.", 500, ErrorType.Failure),
            [ErrorCode.AI_UNAUTHORIZED] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_UNAUTHORIZED),
                "Falha de autenticação com o serviço de IA.", 500, ErrorType.Failure),
            [ErrorCode.AI_RATE_LIMITED] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_RATE_LIMITED),
                "Limite de requisições ao serviço de IA excedido. Tente novamente mais tarde.", 500, ErrorType.Failure),
            [ErrorCode.AI_OVERLOADED] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_OVERLOADED),
                "O serviço de IA está sobrecarregado no momento.", 500, ErrorType.Failure),
            [ErrorCode.AI_TIMEOUT] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_TIMEOUT),
                "O serviço de IA demorou mais que o esperado para responder.", 500, ErrorType.Failure),
            [ErrorCode.AI_PARSE_ERROR] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_PARSE_ERROR),
                "A resposta do serviço de IA não pôde ser lida.", 500, ErrorType.Failure),
            [ErrorCode.AI_UNAVAILABLE] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_UNAVAILABLE),
                "O serviço de IA está indisponível no momento.", 500, ErrorType.Failure),
            [ErrorCode.AI_INVALID_RESPONSE] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_INVALID_RESPONSE),
                "A resposta da IA não pôde ser validada e foi descartada.", 500, ErrorType.Failure),
            [ErrorCode.AI_PROMPT_NOT_FOUND] = new(ErrorGroup.Ai, nameof(ErrorCode.AI_PROMPT_NOT_FOUND),
                "O prompt '{0}' não foi encontrado.", 500, ErrorType.Failure),

            // INTERNAL
            [ErrorCode.INTERNAL_UNEXPECTED] = new(ErrorGroup.Internal, nameof(ErrorCode.INTERNAL_UNEXPECTED),
                "Ocorreu um erro interno inesperado.", 500, ErrorType.Failure),
            [ErrorCode.INTERNAL_DATABASE_ERROR] = new(ErrorGroup.Internal, nameof(ErrorCode.INTERNAL_DATABASE_ERROR),
                "Erro ao acessar o banco de dados.", 500, ErrorType.Failure),
            [ErrorCode.INTERNAL_SERIALIZATION_ERROR] = new(ErrorGroup.Internal, nameof(ErrorCode.INTERNAL_SERIALIZATION_ERROR),
                "Erro ao processar os dados da requisição.", 500, ErrorType.Failure),
        };

    public static IReadOnlyDictionary<ErrorCode, ErrorDefinition> All => Definitions;

    public static ErrorDefinition Get(ErrorCode code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Nenhuma ErrorDefinition registrada para {code} — todo ErrorCode precisa de uma entrada em ErrorCatalog.");

    public static bool TryGet(ErrorCode code, out ErrorDefinition? definition) => Definitions.TryGetValue(code, out definition);
}

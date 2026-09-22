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

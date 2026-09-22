namespace Advocacia.BuildingBlocks.Domain.Errors.Exceptions;

/// <summary>
/// Atalho para <see cref="AppException"/> com um código do grupo <see cref="ErrorGroup.Integration"/>
/// (ex.: <see cref="ErrorCode.INTEGRATION_UNAVAILABLE"/>, <see cref="ErrorCode.INTEGRATION_TIMEOUT"/>)
/// — diferente dos outros atalhos, o código não é fixo: cada integração externa (Supabase,
/// CNJ, Anthropic, ...) escolhe o mais específico para a falha ocorrida.
/// </summary>
public sealed class IntegrationException(ErrorCode code, string? customMessage = null, Exception? inner = null)
    : AppException(code, customMessage, inner);

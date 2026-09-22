namespace Advocacia.BuildingBlocks.Domain.Errors.Exceptions;

/// <summary>Atalho para <see cref="AppException"/> com <see cref="ErrorCode.AUTHORIZATION_FORBIDDEN"/>.</summary>
public sealed class ForbiddenException(string? customMessage = null, Exception? inner = null)
    : AppException(ErrorCode.AUTHORIZATION_FORBIDDEN, customMessage, inner);

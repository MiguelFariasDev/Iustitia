namespace Advocacia.BuildingBlocks.Domain.Errors.Exceptions;

/// <summary>Atalho para <see cref="AppException"/> com <see cref="ErrorCode.COMMON_NOT_FOUND"/>.</summary>
public sealed class NotFoundException(string? customMessage = null, Exception? inner = null)
    : AppException(ErrorCode.COMMON_NOT_FOUND, customMessage, inner);

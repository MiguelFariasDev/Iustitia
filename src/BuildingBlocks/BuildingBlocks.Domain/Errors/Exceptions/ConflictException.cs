namespace Advocacia.BuildingBlocks.Domain.Errors.Exceptions;

/// <summary>Atalho para <see cref="AppException"/> com <see cref="ErrorCode.COMMON_ALREADY_EXISTS"/>.</summary>
public sealed class ConflictException(string? customMessage = null, Exception? inner = null)
    : AppException(ErrorCode.COMMON_ALREADY_EXISTS, customMessage, inner);

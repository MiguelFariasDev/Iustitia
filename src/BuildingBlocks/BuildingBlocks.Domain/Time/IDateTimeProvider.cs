namespace Advocacia.BuildingBlocks.Domain.Time;

/// <summary>
/// Abstrai o relógio do sistema para tornar testável qualquer regra de domínio
/// sensível ao tempo (prazos, expiração, auditoria). Nunca use DateTime.Now/UtcNow
/// diretamente em regras de negócio — injete esta interface.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}

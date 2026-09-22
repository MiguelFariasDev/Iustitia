using Advocacia.BuildingBlocks.Domain.Time;

namespace Advocacia.BuildingBlocks.Infrastructure.Time;

/// <summary>Implementação real de <see cref="IDateTimeProvider"/> sobre o relógio do sistema.</summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Domain.Publications;

/// <summary>
/// Data/hora de disponibilização da publicação no diário, sempre em UTC. Uma data no
/// futuro é dado corrompido — a contagem de prazos (Fase 3) parte daqui, então aceitar
/// isso silenciosamente produziria prazos errados, o pior tipo de erro neste sistema.
///
/// Usa <see cref="DateTimeOffset"/> (e não DateTime) para casar com o resto do domínio
/// (IAuditable) e com o mapeamento nativo do Npgsql para <c>timestamptz</c>, que rejeita
/// DateTime sem Kind=Utc.
/// </summary>
public sealed class PublishedAt : ValueObject
{
    /// <summary>
    /// Folga para desalinhamento de relógio entre o CNJ, o banco e a aplicação. Sem ela,
    /// uma publicação disponibilizada "hoje" por um tribunal alguns minutos adiantado seria
    /// rejeitada.
    /// </summary>
    public static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    public DateTimeOffset Value { get; }

    private PublishedAt(DateTimeOffset value)
    {
        Value = value;
    }

    public static Result<PublishedAt> Create(DateTimeOffset publishedAt, DateTimeOffset utcNow)
    {
        var normalized = publishedAt.ToUniversalTime();

        if (normalized > utcNow.ToUniversalTime() + ClockSkewTolerance)
        {
            return Result.Failure<PublishedAt>(ErrorFactory.From(ErrorCode.PUBLICATION_INVALID_DATE));
        }

        return Result.Success(new PublishedAt(normalized));
    }

    /// <summary>Reconstrói a partir do banco, sem revalidar (ver RawContent.FromPersistence).</summary>
    public static PublishedAt FromPersistence(DateTimeOffset value) => new(value.ToUniversalTime());

    public DateOnly ToDateOnly() => DateOnly.FromDateTime(Value.UtcDateTime);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString("O");
}

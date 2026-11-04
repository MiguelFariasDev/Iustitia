using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Application.UnitTests;

/// <summary>Dados válidos reutilizados pelos testes de handlers (ver Legal.Domain.UnitTests para os equivalentes de domínio).</summary>
internal static class TestPublications
{
    internal const string ValidCnjNumber = "1234567-48.2026.8.26.0001";

    internal static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    internal static Publication Create(
        Guid tenantId,
        string externalId = "cnj-1",
        string cnjNumber = ValidCnjNumber,
        string content = "Intimação para manifestação no prazo legal.") =>
        Publication.Create(
            tenantId,
            CNJNumber.Create(cnjNumber).Value,
            externalId,
            PublicationSource.DJEN,
            RawContent.Create(content).Value,
            PublishedAt.Create(Now.AddDays(-1), Now).Value,
            capturedAt: Now).Value;

    /// <summary>Relógio fixo em <see cref="Now"/>, para as regras sensíveis a tempo serem determinísticas.</summary>
    internal sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => Now;

        public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);
    }
}

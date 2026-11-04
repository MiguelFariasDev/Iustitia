using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Publications;

/// <summary>
/// Dados válidos reutilizados pelos testes. Os números CNJ aqui têm dígitos verificadores
/// corretos (mod 97, ISO 7064) — um número "bonito" inventado na hora seria rejeitado por
/// <see cref="CNJNumber.Create"/>.
/// </summary>
internal static class TestPublications
{
    internal const string ValidCnjNumber = "1234567-48.2026.8.26.0001";
    internal const string ValidCnjNumberUnmasked = "12345674820268260001";
    internal const string OtherValidCnjNumber = "7654321-87.2025.8.19.0500";

    internal static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    internal static CNJNumber CnjNumber(string value = ValidCnjNumber) => CNJNumber.Create(value).Value;

    internal static RawContent RawContent(string value = "Intimação para manifestação no prazo legal.") =>
        Domain.Publications.RawContent.Create(value).Value;

    internal static PublishedAt PublishedAt(DateTimeOffset? value = null) =>
        Domain.Publications.PublishedAt.Create(value ?? Now.AddDays(-1), Now).Value;

    internal static Publication Create(
        Guid? tenantId = null,
        string externalId = "cnj-1",
        string cnjNumber = ValidCnjNumber,
        DateTimeOffset? publishedAt = null) =>
        Publication.Create(
            tenantId ?? Guid.NewGuid(),
            CnjNumber(cnjNumber),
            externalId,
            PublicationSource.DJEN,
            RawContent(),
            PublishedAt(publishedAt),
            capturedAt: Now).Value;
}

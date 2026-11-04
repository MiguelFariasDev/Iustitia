using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.IntegrationTests.Common;

/// <summary>Números CNJ com dígitos verificadores válidos (mod 97, ISO 7064) e fábricas de Publication.</summary>
internal static class TestPublications
{
    internal const string CnjNumberA = "1234567-48.2026.8.26.0001";
    internal const string CnjNumberB = "7654321-87.2025.8.19.0500";
    internal const string CnjNumberC = "0000011-13.2024.5.02.0000";

    internal static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    internal static Publication Create(
        Guid tenantId,
        string externalId,
        string cnjNumber = CnjNumberA,
        Guid? processId = null,
        DateTimeOffset? publishedAt = null,
        string content = "Fica a parte intimada a manifestar-se no prazo de 15 dias.")
    {
        var publication = Publication.Create(
            tenantId,
            CNJNumber.Create(cnjNumber).Value,
            externalId,
            PublicationSource.DJEN,
            RawContent.Create(content).Value,
            PublishedAt.Create(publishedAt ?? Now.AddDays(-1), Now).Value,
            capturedAt: Now).Value;

        if (processId is { } id)
        {
            publication.AttachToProcess(id);
        }

        return publication;
    }
}

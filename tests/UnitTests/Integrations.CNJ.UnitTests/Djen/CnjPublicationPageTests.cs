using Advocacia.Modules.Integrations.CNJ.Application.Models;
using FluentAssertions;

namespace Advocacia.Modules.Integrations.CNJ.UnitTests.Djen;

public class CnjPublicationPageTests
{
    private static CnjPublicationDto Item(string externalId) =>
        new(externalId, "1234567-48.2026.8.26.0001", "TJSP", "Intimação", "texto",
            new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero), MetadataJson: null);

    [Fact]
    public void HasMorePages_WithKnownTotal_ComparesAgainstTheTotal()
    {
        var page = new CnjPublicationPage([Item("1")], Page: 1, PageSize: 10, TotalCount: 25);

        page.HasMorePages.Should().BeTrue();
    }

    [Fact]
    public void HasMorePages_OnAPartialLastPage_IsFalse()
    {
        var page = new CnjPublicationPage([Item("1")], Page: 3, PageSize: 10, TotalCount: 25);

        page.HasMorePages.Should().BeFalse();
    }

    [Fact]
    public void HasMorePages_WithALyingTotalCount_StillContinuesOnAFullPage()
    {
        // O DJEN devolve count = tamanho da página quando itensPorPagina=100. Confiar nele
        // fazia a captura parar na página 1 e perder ~14 mil publicações em silêncio
        // (medido na Etapa 1.3 — ver docs/poc/cnj-poc-report.md).
        var page = new CnjPublicationPage(
            [.. Enumerable.Range(1, 100).Select(i => Item(i.ToString()))],
            Page: 1, PageSize: 100, TotalCount: 100);

        page.HasMorePages.Should().BeTrue();
        page.HasSuspiciousTotalCount.Should().BeTrue();
    }

    [Fact]
    public void HasSuspiciousTotalCount_IsFalseWhenTheTotalLooksReal()
    {
        new CnjPublicationPage(
            [.. Enumerable.Range(1, 50).Select(i => Item(i.ToString()))],
            Page: 1, PageSize: 50, TotalCount: 14138)
            .HasSuspiciousTotalCount.Should().BeFalse();
    }

    [Fact]
    public void HasMorePages_WithoutTotal_AssumesMoreWhenThePageIsFull()
    {
        // O CNJ nem sempre devolve count confiável em consultas amplas; uma página cheia é o
        // único sinal disponível de que ainda há resultados.
        var page = new CnjPublicationPage([Item("1"), Item("2")], Page: 1, PageSize: 2, TotalCount: 0);

        page.HasMorePages.Should().BeTrue();
    }

    [Fact]
    public void HasMorePages_WithoutTotal_IsFalseWhenThePageIsNotFull()
    {
        var page = new CnjPublicationPage([Item("1")], Page: 1, PageSize: 10, TotalCount: 0);

        page.HasMorePages.Should().BeFalse();
    }

    [Fact]
    public void Empty_HasNoItemsAndNoMorePages()
    {
        var page = CnjPublicationPage.Empty(page: 1, pageSize: 100);

        page.Items.Should().BeEmpty();
        page.HasMorePages.Should().BeFalse();
    }
}

using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Publications;

public class RawContentTests
{
    [Fact]
    public void Create_StripsHtmlTags()
    {
        var result = RawContent.Create("<p>Intimação para <strong>manifestação</strong>.</p>");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("Intimação para manifestação .");
    }

    [Fact]
    public void Create_DecodesHtmlEntities()
    {
        var result = RawContent.Create("Autor &amp; R&eacute;u");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("Autor & Réu");
    }

    [Fact]
    public void Create_CollapsesWhitespaceIncludingNonBreakingSpaces()
    {
        var result = RawContent.Create("  Intimação \n\t  para\r\n manifestação&nbsp;&nbsp;imediata.  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("Intimação para manifestação imediata.");
    }

    [Fact]
    public void Create_ReplacesTagsWithASpace_SoWordsAreNotGluedTogether()
    {
        // <br/> sem espaço ao redor: remover a tag sem deixar separador produziria
        // "prazoDe" e quebraria a busca full-text.
        var result = RawContent.Create("prazo<br/>de 15 dias");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("prazo de 15 dias");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("<p></p>")]
    [InlineData("<div> <br/> </div>")]
    public void Create_WithEmptyOrMarkupOnlyContent_Fails(string? input)
    {
        var result = RawContent.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_EMPTY_CONTENT));
    }

    [Fact]
    public void Create_AboveMaxLength_Fails()
    {
        var result = RawContent.Create(new string('a', RawContent.MaxLength + 1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_MAX_LENGTH_EXCEEDED));
    }

    [Fact]
    public void FromPersistence_DoesNotRenormalize()
    {
        // O valor no banco já está normalizado; revalidar a cada materialização custaria
        // regex em toda query (ver PublicationConfiguration).
        var content = RawContent.FromPersistence("<p>já normalizado</p>");

        content.Value.Should().Be("<p>já normalizado</p>");
    }

    [Fact]
    public void ToString_NeverExposesTheContent()
    {
        // Proteção contra vazamento de dado sigiloso em log estruturado ou mensagem de
        // exceção, que é o caminho mais fácil para o texto escapar (ver claude.md, seção 14).
        var content = RawContent.Create("Dados sigilosos da parte: CPF 123.456.789-00").Value;

        content.ToString().Should().NotContain("123.456.789-00");
        content.ToString().Should().NotContain("sigilosos");
    }

    [Fact]
    public void Equality_IsBasedOnTheNormalizedValue()
    {
        var one = RawContent.Create("<p>Intimação</p>").Value;
        var other = RawContent.Create("  Intimação  ").Value;

        one.Should().Be(other);
    }
}

using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Publications;

public class CNJNumberTests
{
    [Fact]
    public void Create_WithMaskedNumber_Succeeds()
    {
        var result = CNJNumber.Create(TestPublications.ValidCnjNumber);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(TestPublications.ValidCnjNumber);
        result.Value.Digits.Should().Be(TestPublications.ValidCnjNumberUnmasked);
    }

    [Fact]
    public void Create_WithUnmaskedNumber_NormalizesToMaskedForm()
    {
        var result = CNJNumber.Create(TestPublications.ValidCnjNumberUnmasked);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(TestPublications.ValidCnjNumber);
    }

    [Theory]
    [InlineData("1234567-48.2026.8.26.0001")]
    [InlineData("12345674820268260001")]
    [InlineData("1234567 48 2026 8 26 0001")]
    [InlineData("1234567/48/2026/8/26/0001")]
    public void Create_IgnoresSeparators_ProducingTheSameValue(string input)
    {
        var result = CNJNumber.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(TestPublications.ValidCnjNumber);
    }

    [Fact]
    public void Create_DecomposesTheNumberIntoItsParts()
    {
        var cnjNumber = CNJNumber.Create(TestPublications.ValidCnjNumber).Value;

        cnjNumber.Sequential.Should().Be(1234567);
        cnjNumber.Year.Should().Be(2026);
        cnjNumber.Segment.Should().Be(8);
        cnjNumber.Court.Should().Be("26");
        cnjNumber.OriginUnit.Should().Be("0001");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("123456789012345678901")]
    [InlineData("abcdefghijklmnopqrst")]
    public void Create_WithMalformedNumber_Fails(string? input)
    {
        var result = CNJNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_CNJ));
    }

    [Fact]
    public void Create_WithWrongCheckDigits_Fails()
    {
        // Mesmo número válido com os dígitos verificadores trocados de 48 para 49.
        var result = CNJNumber.Create("1234567-49.2026.8.26.0001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_CNJ));
    }

    [Fact]
    public void Create_WithSegmentZero_Fails()
    {
        // Segmento 0 não existe na Resolução CNJ 65/2008 (só 1 a 9).
        var result = CNJNumber.Create("1234567-48.2026.0.26.0001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_CNJ));
    }

    [Fact]
    public void Equality_IsBasedOnDigits_NotOnTheInputFormatting()
    {
        var masked = CNJNumber.Create(TestPublications.ValidCnjNumber).Value;
        var unmasked = CNJNumber.Create(TestPublications.ValidCnjNumberUnmasked).Value;

        masked.Should().Be(unmasked);
        masked.GetHashCode().Should().Be(unmasked.GetHashCode());
    }

    [Fact]
    public void Equality_IsFalseForDifferentNumbers()
    {
        var one = CNJNumber.Create(TestPublications.ValidCnjNumber).Value;
        var other = CNJNumber.Create(TestPublications.OtherValidCnjNumber).Value;

        one.Should().NotBe(other);
    }
}

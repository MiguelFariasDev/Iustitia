using Advocacia.Modules.Legal.Infrastructure.Consulta;
using FluentAssertions;

namespace Advocacia.Platform.IntegrationTests;

/// <summary>Como o telefone do WhatsApp (com DDI) é casado com o cadastro do Legal (sem DDI).</summary>
public class BrazilianPhoneTests
{
    [Theory]
    [InlineData("5585999998888", new[] { "85999998888", "8599998888" })]   // celular com 9: tenta também sem o 9
    [InlineData("558599998888", new[] { "8599998888", "85999998888" })]    // sem o 9: tenta também com o 9
    [InlineData("+55 (85) 99999-8888", new[] { "85999998888", "8599998888" })]
    [InlineData("558533334444", new[] { "8533334444", "85933334444" })]    // fixo
    public void BrazilianNumbers_YieldTheNationalFormAndTheNinthDigitVariant(string wa, string[] expected)
    {
        BrazilianPhone.NationalCandidates(wa).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("14155552671")]          // EUA: o cadastro não guarda DDI, não pode cruzar com um brasileiro
    [InlineData("447911123456")]
    [InlineData("85999998888")]          // sem DDI: não é wa_id
    [InlineData("5585")]
    [InlineData("55859999988889999")]
    [InlineData("")]
    [InlineData(null)]
    public void NonBrazilianOrMalformedNumbers_NeverMatchAnything(string? wa)
    {
        BrazilianPhone.NationalCandidates(wa).Should().BeEmpty();
    }
}

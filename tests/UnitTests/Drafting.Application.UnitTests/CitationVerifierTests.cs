using Advocacia.Modules.Drafting.Application.Services;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class CitationVerifierTests
{
    private static readonly string Caso = "O autor pagou antecipadamente. Pede a condenação.";

    [Fact]
    public void ReconheceCitacaoDeFonteRecuperada_IgnorandoCaixaEPontuacao()
    {
        var chunk = Samples.Chunk("Art. 186 do Código Civil: aquele que causar dano comete ato ilícito.", "Contrato.pdf", 3);

        var report = CitationVerifier.Verify("Há ato ilícito [contrato.PDF, p. 3].", [chunk], Caso);

        report.UnknownCitations.Should().BeEmpty();
        CitationVerifier.WasCited(chunk, report).Should().BeTrue();
    }

    [Fact]
    public void ApontaCitacaoQueNaoExisteNoContexto()
    {
        var chunk = Samples.Chunk(arquivo: "contrato.pdf", pagina: 3);

        var report = CitationVerifier.Verify("Conforme [inventado.pdf, p. 9] e [contrato.pdf, p. 3].", [chunk], Caso);

        report.UnknownCitations.Should().ContainSingle().Which.Should().Be("inventado.pdf, p. 9");
        CitationVerifier.WasCited(chunk, report).Should().BeTrue();
    }

    [Fact]
    public void PaginaDiferenteDaRecuperadaContaComoCitacaoDesconhecida()
    {
        var chunk = Samples.Chunk(arquivo: "contrato.pdf", pagina: 3);

        var report = CitationVerifier.Verify("Veja [contrato.pdf, p. 4].", [chunk], Caso);

        report.UnknownCitations.Should().ContainSingle();
        CitationVerifier.WasCited(chunk, report).Should().BeFalse();
    }

    [Fact]
    public void FonteNaoCitadaFicaMarcadaComoNaoCitada()
    {
        var chunk = Samples.Chunk();

        var report = CitationVerifier.Verify("Texto sem nenhuma citação.", [chunk], Caso);

        CitationVerifier.WasCited(chunk, report).Should().BeFalse();
        report.CitedLabels.Should().BeEmpty();
    }

    [Fact]
    public void ApontaArtigoESumulaSemRespaldoNasFontesNemNoCaso()
    {
        var chunk = Samples.Chunk("Art. 186 do Código Civil: ato ilícito.");

        var report = CitationVerifier.Verify(
            "Nos termos do art. 186 e do art. 927 do CC, e da Súmula 297 do STJ.", [chunk], Caso);

        report.UnsupportedReferences.Should().BeEquivalentTo("art. 927", "Súmula 297");
    }

    [Fact]
    public void ReferenciaPresenteNosFatosDoUsuarioNaoEhAlarme()
    {
        var report = CitationVerifier.Verify(
            "Aplica-se o art. 5º da lei citada.", [], "O contrato cita o art. 5º como base.");

        report.UnsupportedReferences.Should().BeEmpty();
    }

    [Fact]
    public void TextoSemReferenciasNemCitacoes_NaoGeraNada()
    {
        var report = CitationVerifier.Verify("FUNDAMENTO PENDENTE DE VERIFICAÇÃO", [], Caso);

        report.CitedLabels.Should().BeEmpty();
        report.UnknownCitations.Should().BeEmpty();
        report.UnsupportedReferences.Should().BeEmpty();
    }
}

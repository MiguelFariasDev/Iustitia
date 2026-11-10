using Advocacia.Modules.Drafting.Infrastructure.Ollama;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Infrastructure.UnitTests;

public class ThinkTagFilterTests
{
    private static string Run(params string[] pieces)
    {
        var filter = new ThinkTagFilter();
        var text = string.Concat(pieces.Select(filter.Push));

        return text + filter.Flush();
    }

    [Fact]
    public void TextoSemTagPassaIntacto() => Run("DOS ", "FATOS").Should().Be("DOS FATOS");

    [Fact]
    public void RemoveOBlocoThinkInteiro() =>
        Run("<think>pensando…</think>DOS FATOS").Should().Be("DOS FATOS");

    [Fact]
    public void RemoveOBlocoQuandoAsTagsChegamPartidasByteAByte()
    {
        var entrada = "<think>x</think>Texto";

        Run(entrada.Select(c => c.ToString()).ToArray()).Should().Be("Texto");
    }

    [Fact]
    public void DescartaOsEspacosEmBrancoDepoisDoBloco() =>
        Run("<think>a</think>\n\n  Texto").Should().Be("Texto");

    [Fact]
    public void MantemOsEspacosDoMeioDoTexto() => Run("a ", " b").Should().Be("a  b");

    [Fact]
    public void BlocoNuncaFechadoEhDescartadoNoFlush() => Run("Antes<think>nunca fecha").Should().Be("Antes");

    [Fact]
    public void MenorQueQueNaoEhTagVoltaComoTexto() => Run("a < b e c <d").Should().Be("a < b e c <d");

    [Fact]
    public void PrefixoDeTagNoFimDoStreamEhDevolvidoNoFlush() => Run("Fim <thi").Should().Be("Fim <thi");

    [Fact]
    public void VariosBlocosNoMesmoTexto() =>
        Run("<think>1</think>A<think>2</think>B").Should().Be("AB");
}

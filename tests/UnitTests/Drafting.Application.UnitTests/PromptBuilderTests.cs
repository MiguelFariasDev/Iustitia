using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void SystemPrompt_TrazAsQuinzeRegrasAbsolutasEAEstruturaObrigatoria()
    {
        var bundle = _builder.Build(Samples.Input(), []);

        bundle.System.Should().StartWith("Você é um assistente de redação jurídica brasileira.");
        bundle.System.Should().Contain("Regras absolutas:");
        for (var regra = 1; regra <= 15; regra++)
        {
            bundle.System.Should().Contain($"{regra}. ");
        }

        bundle.System.Should().Contain("[ARQUIVO, p. X] ou [NORMA, art. X]");
        bundle.System.Should().Contain("Não apresente norma revogada como vigente sem indicar a data");
        bundle.System.Should().Contain("“FUNDAMENTO PENDENTE DE VERIFICAÇÃO”");
        bundle.System.Should().Contain("“FONTES UTILIZADAS”");
        bundle.System.Should().Contain("“PONTOS PARA REVISÃO HUMANA”");
        bundle.System.Should().Contain("afirme que ele está pronto para protocolo");
        foreach (var secao in new[]
                 {
                     "ENDEREÇAMENTO", "DOS FATOS", "DO DIREITO", "DOS PEDIDOS", "DAS PROVAS", "DOS REQUERIMENTOS",
                     "FONTES UTILIZADAS", "PONTOS PARA REVISÃO HUMANA",
                 })
        {
            bundle.System.Should().Contain($"- {secao}");
        }
    }

    [Fact]
    public void SystemPrompt_NuncaContemTextoDoUsuarioNemDoContexto()
    {
        var input = Samples.Input("FATO-EXCLUSIVO-DO-USUARIO");
        var bundle = _builder.Build(input, [Samples.Chunk("TRECHO-EXCLUSIVO-DO-CONTEXTO")]);

        bundle.System.Should().NotContain("FATO-EXCLUSIVO-DO-USUARIO");
        bundle.System.Should().NotContain("TRECHO-EXCLUSIVO-DO-CONTEXTO");
        bundle.System.Should().Be(SystemPrompt.Texto);
        bundle.Versao.Should().Be(SystemPrompt.Versao);
    }

    [Fact]
    public void UserPrompt_SeguaOFormatoDefinidoEFicaNaOrdem()
    {
        var chunk = Samples.Chunk("Cláusula 5: multa de 10%.", "contrato.pdf", 3);
        var bundle = _builder.Build(
            Samples.Input() with { InstrucoesAdicionais = "Tom firme." }, [chunk]);

        bundle.User.Should().Be(
            "TIPO DE PEÇA:\npetição inicial\n\n" +
            "ÁREA:\ncível\n\n" +
            "FATOS:\nO cliente contratou o serviço e não recebeu.\n\n" +
            "PEDIDOS:\nCondenação ao pagamento de R$ 5.000,00.\n\n" +
            "INSTRUÇÕES ADICIONAIS:\nTom firme.\n\n" +
            "CONTEXTO RECUPERADO:\n[contrato.pdf, p. 3]\nCláusula 5: multa de 10%.\n\n" +
            "TAREFA:\nRedija uma minuta de petição inicial com base exclusivamente nas informações acima. " +
            "Não invente fatos, provas ou fundamentos jurídicos. Destaque expressamente as informações ausentes.");
    }

    [Fact]
    public void UserPrompt_ListaOsTrechosNaOrdemRecebidaECadaUmComSeuRotulo()
    {
        var bundle = _builder.Build(
            Samples.Input(), [Samples.Chunk("Primeiro.", "a.pdf", 1), Samples.Chunk("Segundo.", "b.pdf", null)]);

        bundle.User.Should().Contain("[a.pdf, p. 1]\nPrimeiro.\n\n[b.pdf, p. s/n]\nSegundo.");
        bundle.User.IndexOf("Primeiro.", StringComparison.Ordinal)
            .Should().BeLessThan(bundle.User.IndexOf("Segundo.", StringComparison.Ordinal));
    }

    [Fact]
    public void SemInstrucoesNemContexto_DizIssoExplicitamente()
    {
        var bundle = _builder.Build(Samples.Input(), []);

        bundle.User.Should().Contain($"INSTRUÇÕES ADICIONAIS:\n{PromptBuilder.SemInstrucoes}");
        bundle.User.Should().Contain($"CONTEXTO RECUPERADO:\n{PromptBuilder.SemContexto}");
    }

    [Fact]
    public void SemPedidos_DizIssoExplicitamente_ParaOModeloNaoInventarPedidos()
    {
        var bundle = _builder.Build(Samples.Input() with { Pedidos = string.Empty }, []);

        bundle.User.Should().Contain($"PEDIDOS:\n{PromptBuilder.SemPedidos}");
    }

    [Fact]
    public void Build_EhDeterministico()
    {
        var chunks = new[] { Samples.Chunk(), Samples.Chunk("Outro.", "x.pdf", 9) };

        _builder.Build(Samples.Input(), chunks).User.Should().Be(_builder.Build(Samples.Input(), chunks).User);
    }

    [Fact]
    public void RotuloDeCitacao_NaoQuebraOFormatoComColchetesEVirgulasNoNomeDoArquivo()
    {
        CitationLabel.For("laudo [final], v2.pdf", 4).Should().Be("laudo (final); v2.pdf, p. 4");
    }
}

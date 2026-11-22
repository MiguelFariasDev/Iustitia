using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Validacao;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Pecas;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class ValidadorCitacoesTests
{
    private static ValidacaoCitacoes Validar(string texto, string caso = "", DateOnly? fatos = null, IReadOnlySet<string>? urns = null, params VectorHit[] hits) =>
        ValidadorCitacoes.Validar(texto, new ContextoValidacao(hits.Select(Samples.Chunk).ToList(), caso, fatos, urns));

    private static CitacaoStatus StatusDe(ValidacaoCitacoes v, string trecho) =>
        v.Itens.Should().Contain(i => i.Texto.Contains(trecho, StringComparison.OrdinalIgnoreCase), because: "item esperado").Which.Status;

    // ---------------------------------------------------------------- citação que confere

    [Theory]
    [InlineData("[Código Civil, art. 186]")]
    [InlineData("[código civil, Art 186]")]
    [InlineData("[CC, art. 186]")]
    [InlineData("[Código Civil, art. 186, caput]")]
    [InlineData("[Lei nº 10.406/2002, art. 186]")]
    public void FormasDeCitarOMesmoDispositivo_SaoValidadas(string citacao)
    {
        var v = Validar($"Há ato ilícito {citacao}.", hits: Samples.HitNorma());

        v.Itens.Should().ContainSingle().Which.Status.Should().Be(CitacaoStatus.Validada);
        v.ExportacaoBloqueada.Should().BeFalse();
        v.Cita(Samples.Chunk(Samples.HitNorma())).Should().BeTrue();
    }

    [Fact]
    public void VariosArtigos_TodosRecuperados_Valida_FaltandoUm_Pendente()
    {
        var a = Samples.HitNorma(artigo: "186");
        var b = Samples.HitNorma(artigo: "927", texto: "Art. 927", normaId: a.Meta.Norma!.NormaId);

        Validar("[Código Civil, arts. 186 e 927]", hits: [a, b]).Itens.Single().Status.Should().Be(CitacaoStatus.Validada);
        Validar("[Código Civil, arts. 186 e 927]", hits: a).Itens.Single().Status.Should().Be(CitacaoStatus.PendenteDeVerificacao);
    }

    [Fact]
    public void ParagrafoNaoEOutroArtigo()
    {
        Validar("[Código Civil, art. 186, § 2º]", hits: Samples.HitNorma()).Itens.Single().Status.Should().Be(CitacaoStatus.Validada);
    }

    [Fact]
    public void DocumentoDoCaso_PaginaCerta_Valida_PaginaErrada_Pendente()
    {
        Validar("[contrato.pdf, p. 3]", hits: Samples.HitCaso()).Itens.Single().Status.Should().Be(CitacaoStatus.Validada);
        Validar("[contrato.pdf, p. 9]", hits: Samples.HitCaso()).Itens.Single().Status.Should().Be(CitacaoStatus.PendenteDeVerificacao);
    }

    [Fact]
    public void DocumentoNaoConferido_EPendente_EBloqueia()
    {
        var v = Validar("[contrato.pdf, p. 3]", hits: Samples.HitCaso(origem: OrigemTexto.Ocr, confianca: 0.4, revisaoPendente: true));

        v.Itens.Single().Status.Should().Be(CitacaoStatus.PendenteDeVerificacao);
        v.ExportacaoBloqueada.Should().BeTrue();
    }

    [Fact]
    public void Jurisprudencia_RotuloExato_Valida_ComNumeroDiferente_Alucinacao()
    {
        Validar("[STJ, Súmula nº 297]", hits: Samples.HitSumula()).Itens.Single().Status.Should().Be(CitacaoStatus.Validada);
        Validar("[STJ, Súmula nº 999]", hits: Samples.HitSumula()).Itens.Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao);
    }

    [Fact]
    public void Placeholder_NaoECitacao()
    {
        Validar("[NOME DO AUTOR], residente em [ENDEREÇO COMPLETO], [FUNDAMENTO PENDENTE DE VERIFICAÇÃO].").Itens.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- invenção

    [Theory]
    [InlineData("[Código de Defesa do Consumidor, art. 14]")]
    [InlineData("[TJCE, Apelação nº 0000000-00.0000.8.06.0000, p. 12]")]
    [InlineData("[inventado.pdf, p. 2]")]
    [InlineData("[STF, RE nº 123456]")]
    public void FonteForaDoContexto_EPossivelAlucinacao(string citacao)
    {
        var v = Validar(citacao, hits: Samples.HitNorma());

        v.Itens.Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao);
        v.ExportacaoBloqueada.Should().BeTrue();
    }

    [Fact]
    public void ArtigoNaoRecuperadoDaNormaPresente_EPendente()
    {
        StatusDe(Validar("[Código Civil, art. 927]", hits: Samples.HitNorma()), "927").Should().Be(CitacaoStatus.PendenteDeVerificacao);
    }

    [Fact]
    public void ReferenciaSemColchetes_TambemEConferida()
    {
        var v = Validar("Nos termos do art. 927 do Código Civil, da Súmula 54 do STJ e do REsp 1.234.567/SP.", hits: Samples.HitNorma());

        StatusDe(v, "927").Should().Be(CitacaoStatus.PendenteDeVerificacao);
        StatusDe(v, "Súmula nº 54").Should().Be(CitacaoStatus.PossivelAlucinacao);
        StatusDe(v, "REsp 1.234.567").Should().Be(CitacaoStatus.PossivelAlucinacao);
    }

    [Fact]
    public void ReferenciaDaPropriaAdvogada_NaoEAlucinacao_MasPrecisaDeConferencia()
    {
        StatusDe(Validar("Aplica-se o art. 14 do CDC.", caso: "Quero invocar o art. 14 do CDC."), "14").Should().Be(CitacaoStatus.PendenteDeVerificacao);
    }

    [Fact]
    public void SumulaDeOutroTribunal_EPossivelAlucinacao()
    {
        Validar("Conforme a Súmula 297 do STF.", hits: Samples.HitSumula()).Itens
            .Should().Contain(i => i.Status == CitacaoStatus.PossivelAlucinacao && i.Detalhe.Contains("outro tribunal"));
    }

    [Fact]
    public void AnoIncoerente_EPossivelAlucinacao()
    {
        Validar("A Lei nº 10.406/2003 dispõe.", hits: Samples.HitNorma()).Itens.Should().Contain(i => i.Detalhe.Contains("ano não confere"));
        Validar("A Lei nº 10.406, de 10 de janeiro de 2002, dispõe.", hits: Samples.HitNorma()).Itens.Should().NotContain(i => i.Detalhe.Contains("ano não confere"));
    }

    [Fact]
    public void AspasEEmentaInventadas_SaoAlucinacao_ReaisPassam()
    {
        var hit = Samples.HitNorma(texto: "Aquele que, por ação ou omissão voluntária, negligência ou imprudência, violar direito e causar dano a outrem comete ato ilícito.");

        Validar("“O dano moral dispensa qualquer prova de culpa do agente causador do prejuízo”.", hits: hit).Itens
            .Should().Contain(i => i.Detalhe.Contains("aspas"));
        Validar("“Aquele que, por ação ou omissão voluntária, (...) violar direito e causar dano a outrem comete ato ilícito.”", hits: hit).Itens
            .Should().NotContain(i => i.Detalhe.Contains("aspas"));
        Validar("EMENTA: APELAÇÃO CÍVEL. DANO MORAL IN RE IPSA. RECURSO PROVIDO EM PARTE.", hits: hit).Itens
            .Should().Contain(i => i.Detalhe.Contains("Ementa"));
    }

    [Fact]
    public void Urn_ForaDoContexto()
    {
        const string urn = "urn:lex:br:federal:lei:1990-09-11;8078";

        Validar($"Veja {urn}.", hits: Samples.HitNorma()).Itens.Should().Contain(i => i.Texto == urn && i.Status == CitacaoStatus.PossivelAlucinacao);
        Validar($"Veja {urn}.", urns: new HashSet<string> { urn }, hits: Samples.HitNorma()).Itens
            .Should().Contain(i => i.Texto == urn && i.Status == CitacaoStatus.PendenteDeVerificacao);
    }

    // ---------------------------------------------------------------- vigência

    [Fact]
    public void NormaRevogada_SemContextoTemporal_EMarcada_ComContexto_Passa()
    {
        var hit = Samples.HitNorma(revogada: true, situacao: "Revogada");

        Validar("Aplica-se [Código Civil, art. 186].", hits: hit).Itens.Single().Status.Should().Be(CitacaoStatus.NormaRevogada);
        Validar("A norma, hoje revogada, previa [Código Civil, art. 186].", hits: hit).Itens.Single().Status.Should().Be(CitacaoStatus.Validada);
    }

    [Fact]
    public void SumulaCancelada_ContaComoForaDeVigor()
    {
        var hit = Samples.HitNorma("Súmula nº 1 do STJ", "Súmula", "1", null, "texto", colecao: Colecoes.Jurisprudencia, situacao: "Cancelado", tribunal: "STJ");

        Validar("[STJ, Súmula nº 1]", hits: hit).Itens.Single().Status.Should().Be(CitacaoStatus.NormaRevogada);
    }

    [Fact]
    public void NormaPosteriorAosFatos_EConflitoDeVersao()
    {
        Validar("[Código Civil, art. 186]", fatos: new DateOnly(1999, 5, 1), hits: Samples.HitNorma()).Itens.Single()
            .Status.Should().Be(CitacaoStatus.ConflitoDeVersao);
        Validar("[Código Civil, art. 186]", fatos: new DateOnly(2020, 5, 1), hits: Samples.HitNorma()).Itens.Single()
            .Status.Should().Be(CitacaoStatus.Validada);
    }

    [Fact]
    public void DuasVersoesDaMesmaNormaNoContexto_EConflitoDeVersao()
    {
        var id = Guid.NewGuid();

        Validar(
            "[Código Civil, art. 186]",
            hits: [Samples.HitNorma(normaId: id, versao: 1), Samples.HitNorma(normaId: id, versao: 2, artigo: "187", texto: "outro")])
            .Itens.Single().Status.Should().Be(CitacaoStatus.ConflitoDeVersao);
    }

    // ---------------------------------------------------------------- fatos

    [Fact]
    public void FatoSemSuporte_ValorDataProcesso_FicaPendente()
    {
        var v = Validar(
            "DOS FATOS\nEm 10/03/2024 pagou R$ 50.000,00 (processo 0001234-56.2024.8.06.0001) e em 15 de abril de 2024, R$ 900,00.",
            caso: "Pagou R$ 50.000 em 10 de março de 2024.");

        v.Itens.Select(i => i.Texto).Should().BeEquivalentTo("0001234-56.2024.8.06.0001", "15 de abril de 2024", "R$ 900,00");
        v.Itens.Should().OnlyContain(i => i.Status == CitacaoStatus.PendenteDeVerificacao && i.Detalhe.Contains("sem suporte"));
    }

    [Fact]
    public void FatoPresenteNoAnexo_TemSuporte()
    {
        Validar("O contrato previa R$ 1.234,56.", hits: Samples.HitCaso("Valor: R$1.234,56 ao mês.")).Itens.Should().BeEmpty();
    }

    [Fact]
    public void DireitoSemNenhumaCitacao_EPendente_SalvoSeAPropriaMinutaDiz()
    {
        Validar("DO DIREITO\nO pedido procede.", hits: Samples.HitNorma()).Itens.Should().ContainSingle().Which.Texto.Should().Be("DO DIREITO");
        Validar("DO DIREITO\nFUNDAMENTO PENDENTE DE VERIFICAÇÃO", hits: Samples.HitNorma()).Itens.Should().BeEmpty();
    }

    [Fact]
    public void SecaoDeRevisao_NaoGeraFalsoAlarme()
    {
        Validar("DOS FATOS\nNada.\nFONTES UTILIZADAS\n-\nPONTOS PARA REVISÃO HUMANA\nConferir a Súmula 54 do STJ e o valor de R$ 9,00.")
            .Itens.Should().BeEmpty();
    }

    [Theory]
    [InlineData("R$ 1.000", "1000,00")]
    [InlineData("R$1000,0", "1000,00")]
    [InlineData("R$ 1.000,00", "1000,00")]
    public void ValoresIguaisEscritosDeFormaDiferente_SaoIguais(string valor, string normal) =>
        ValidadorCitacoes.NormalizarValor(valor).Should().Be(normal);
}

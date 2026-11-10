using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Normas;
using FluentAssertions;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

public class FrontMatterParserTests
{
    [Fact]
    public void LeEscalaresComAspasNulosEDatasSemAspas()
    {
        var fm = FrontMatterParser.Parse("""
            ---
            titulo: "Lei nº 10.406 de 10/01/2002"
            numero: "10406"
            data: 2002-01-10
            revogada: false
            situacao: null
            vazio:
            citado: "ele disse \"oi\""
            ---
            # Corpo
            """);

        fm.Presente.Should().BeTrue();
        fm.Get("titulo").Should().Be("Lei nº 10.406 de 10/01/2002");
        fm.Get("DATA").Should().Be("2002-01-10", "as chaves não diferenciam caixa");
        fm.Get("revogada").Should().Be("false");
        fm.Get("situacao").Should().BeNull();
        fm.Get("vazio").Should().BeNull();
        fm.Get("citado").Should().Be("ele disse \"oi\"");
        fm.Corpo.Should().StartWith("# Corpo");
    }

    [Fact]
    public void JuntaLinhasDeContinuacaoIndentadas()
    {
        var fm = FrontMatterParser.Parse("---\nementa: \"Primeira parte\n  segunda parte\"\n---\ncorpo");

        fm.Get("ementa").Should().Be("Primeira parte segunda parte");
    }

    [Fact]
    public void SemCabecalhoOuSemFechamento_TudoViraCorpo()
    {
        FrontMatterParser.Parse("# Só texto").Presente.Should().BeFalse();

        var aberto = FrontMatterParser.Parse("---\ntitulo: x\nsem fechamento");
        aberto.Presente.Should().BeFalse();
        aberto.Corpo.Should().Contain("sem fechamento");
    }

    [Fact]
    public void IgnoraBomEComentarios()
    {
        var fm = FrontMatterParser.Parse("﻿---\n# comentário\ntitulo: ok\n---\nx");

        fm.Get("titulo").Should().Be("ok");
    }
}

public class NormaMetadataResolverTests
{
    private const string LeiCivil = """
        ---
        titulo: "Lei nº 10.406 de 10/01/2002"
        tipo: "Lei Ordinária"
        numero: "10406"
        data: 2002-01-10
        ementa: "Institui o Código Civil."
        revogada: false
        versao_texto: "compilada (texto vigente)"
        urn: "urn:lex:br:federal:lei:2002-01-10;10406"
        fonte: "https://normas.leg.br/?urn=urn:lex:br:federal:lei:2002-01-10;10406"
        baixado_em: 2026-10-13
        ---
        # Lei nº 10.406 de 10/01/2002

        Institui o Código Civil.

        **Art. 1º** Toda pessoa é capaz de direitos e deveres na ordem civil.
        """;

    private static NormaMetadata Resolve(string path, string markdown)
    {
        var resolucao = NormaMetadataResolver.Resolver(path, FrontMatterParser.Parse(markdown));
        resolucao.Ok.Should().BeTrue(resolucao.Motivo);

        return resolucao.Metadata!;
    }

    [Fact]
    public void LeiFederal_LidaDoYaml()
    {
        var meta = Resolve("federal/leis-ordinarias/2002/lei-10406-2002-01-10.md", LeiCivil);

        meta.Titulo.Should().Be("Lei nº 10.406 de 10/01/2002");
        meta.Tipo.Should().Be("Lei Ordinária");
        meta.Numero.Should().Be("10406");
        meta.Ano.Should().Be(2002);
        meta.Data.Should().Be(new DateOnly(2002, 1, 10));
        meta.Ementa.Should().Be("Institui o Código Civil.");
        meta.Revogada.Should().BeFalse();
        meta.Situacao.Should().Be("Vigente (conforme a fonte)");
        meta.Urn.Should().Be("urn:lex:br:federal:lei:2002-01-10;10406");
        meta.FonteOficialUrl.Should().StartWith("https://normas.leg.br/");
        meta.Esfera.Should().Be("federal");
        meta.Colecao.Should().Be(Colecoes.LeisFederais);
    }

    [Fact]
    public void Constituicao_SemYaml_DeduzTituloFonteETipo()
    {
        var meta = Resolve("constituicao.md", """
            # Constituição da República Federativa do Brasil de 1988

            > Fonte: [texto compilado do Planalto](https://www.planalto.gov.br/ccivil_03/constituicao/constituicaocompilado.htm), obtido em 2026-10-20.

            ## PREÂMBULO

            Nós, representantes do povo brasileiro, reunidos em Assembléia Nacional Constituinte.
            """);

        meta.Titulo.Should().Be("Constituição da República Federativa do Brasil de 1988");
        meta.Tipo.Should().Be("Constituição");
        meta.Ano.Should().Be(1988);
        meta.FonteOficialUrl.Should().Contain("planalto.gov.br");
        meta.Area.Should().Be("constitucional");
        meta.Colecao.Should().Be(Colecoes.LeisFederais);
        meta.Urn.Should().BeNull("a URN não é inventada");
    }

    [Fact]
    public void SumulaDoStf_VaiParaAColecaoDeSumulasComTribunal()
    {
        var meta = Resolve("jurisprudencia/stf/sumulas/sumula-1.md", """
            ---
            titulo: "Súmula nº 1 do STF"
            tribunal: "STF"
            tipo: "Súmula"
            numero: "1"
            data: 1963-12-13
            revogada: false
            fonte: "https://www.stf.jus.br/arquivo/sumulas.pdf"
            ---
            # Súmula nº 1 do STF

            SÚMULA 1
            É vedada a expulsão de estrangeiro casado com Brasileira.
            """);

        meta.Tribunal.Should().Be("STF");
        meta.Esfera.Should().Be("jurisprudencia");
        meta.Colecao.Should().Be(Colecoes.Sumulas);
    }

    [Fact]
    public void DecisaoSemSumula_VaiParaJurisprudencia()
    {
        var meta = Resolve("jurisprudencia/stj/repetitivos/tema-1.md", """
            ---
            titulo: "Tema repetitivo nº 1 do STJ"
            tribunal: "STJ"
            tipo: "Tema repetitivo"
            numero: "1"
            data: 2012-05-02
            situacao: "Trânsito em Julgado"
            ---
            # Tema repetitivo nº 1 do STJ

            Texto da tese firmada pelo tribunal sobre o tema.
            """);

        meta.Colecao.Should().Be(Colecoes.Jurisprudencia);
        meta.Situacao.Should().Be("Trânsito em Julgado");
    }

    [Fact]
    public void DecretoMunicipal_UsaUfEMunicipioDoYaml_ERevogadaNulaViraNaoInformada()
    {
        var meta = Resolve("municipais/CE/fortaleza/decretos-legislativos/2018/decretos-legislativos-838-2018-sapl-5322.md", """
            ---
            titulo: "Decreto Legislativo nº 838, de 06 de setembro de 2018"
            tipo: "Decreto legislativo"
            numero: "838"
            data: "2018-09-06"
            municipio: "Fortaleza"
            uf: "CE"
            fonte: "https://sapl.fortaleza.ce.leg.br/norma/5322"
            revogada: null
            ---
            # Decreto Legislativo nº 838, de 06 de setembro de 2018

            Concede o Título de Cidadão de Fortaleza ao senhor Luciano Nunes Maia.
            """);

        meta.Esfera.Should().Be("municipal");
        meta.Uf.Should().Be("CE");
        meta.Municipio.Should().Be("Fortaleza");
        meta.Colecao.Should().Be(Colecoes.LegislacaoMunicipal);
        meta.Revogada.Should().BeFalse();
        meta.Situacao.Should().Be("Não informada", "o acervo não disse: não se inventa vigência");
    }

    [Fact]
    public void Tratado_UsaSituacaoIndiceEAceitaDataVazia()
    {
        var meta = Resolve("tratados/oit/convencao-102.md", """
            ---
            titulo: "Convenção OIT nº 102 - Norma Mínima sobre Previdência Social"
            tipo: "Convenção da OIT"
            fonte: "https://www.planalto.gov.br/ccivil_03/_ato2023-2026/2026/decreto/D12936.htm"
            numero: "102"
            data: ""
            situacao_indice: "Vigente"
            ---
            # Convenção OIT nº 102

            A Conferência Geral da Organização Internacional do Trabalho, convocada em Genebra.
            """);

        meta.Situacao.Should().Be("Vigente");
        meta.Data.Should().BeNull();
        meta.Esfera.Should().Be("internacional");
        meta.Colecao.Should().Be(Colecoes.Tratados);
        meta.Area.Should().Be("previdenciario");
    }

    [Fact]
    public void SemTituloNoYaml_UsaOPrimeiroH1_ESemFonteGeraAviso()
    {
        var resolucao = NormaMetadataResolver.Resolver(
            "federal/decretos/2001/x.md",
            FrontMatterParser.Parse("---\ntipo: \"Decreto\"\n---\n# Decreto nº 1 de 2001\n\nTexto do decreto com bastante conteúdo."));

        resolucao.Ok.Should().BeTrue();
        resolucao.Metadata!.Titulo.Should().Be("Decreto nº 1 de 2001");
        resolucao.Metadata.FonteOficialUrl.Should().BeEmpty();
        resolucao.Avisos.Should().Contain(a => a.Contains("Fonte oficial"));
    }

    [Fact]
    public void ArquivoSemConteudo_EhRecusado()
    {
        var resolucao = NormaMetadataResolver.Resolver("federal/x.md", FrontMatterParser.Parse("---\ntitulo: \"X\"\n---\n"));

        resolucao.Ok.Should().BeFalse();
        resolucao.Motivo.Should().Contain("sem conteúdo");
    }

    [Fact]
    public void FonteQueNaoEHttp_EDescartada()
    {
        var resolucao = NormaMetadataResolver.Resolver(
            "federal/x.md",
            FrontMatterParser.Parse("---\ntitulo: \"X\"\nfonte: \"javascript:alert(1)\"\n---\n# X\n\nTexto longo o suficiente para valer."));

        resolucao.Metadata!.FonteOficialUrl.Should().BeEmpty();
    }
}

public class NormaFilePolicyTests
{
    private readonly NormasIngestaoOptions _options = new();

    [Theory]
    [InlineData("constituicao.md", true)]
    [InlineData("federal/leis-ordinarias/2002/lei-10406-2002-01-10.md", true)]
    [InlineData("jurisprudencia/stf/sumulas/sumula-1.md", true)]
    [InlineData("municipais/CE/fortaleza/leis/lei-1.md", true)]
    [InlineData("estaduais/CE/leis/lei-1.md", true)]
    [InlineData("README.md", false)]
    [InlineData("federal/_fonte/auditorias/QA100/dec-1.md", false)]
    [InlineData("federal/leis-ordinarias/_lotes/lote-1.md", false)]
    [InlineData("federal/_auditoria_x.json", false)]
    [InlineData("federal/leis-ordinarias/2002/lei.pdf", false)]
    [InlineData("municipais/CE/amontada/lei-1.md", false)]
    [InlineData("estaduais/BA/leis/lei-1.md", false)]
    [InlineData("scripts/ferramenta.md", false)]
    [InlineData("federal/Scripts/a.md", false)]
    [InlineData("_fonte/x.md", false)]
    public void ElegivelSegueAPoliticaDeDownloadSeletivo(string path, bool esperado) =>
        NormaFilePolicy.Elegivel(path, _options).Should().Be(esperado);

    [Fact]
    public void PastasConfiguradasMudamOEscopo()
    {
        _options.PastasIncluidas = ["estaduais/BA"];

        NormaFilePolicy.Elegivel("estaduais/BA/leis/lei-1.md", _options).Should().BeTrue();
        NormaFilePolicy.Elegivel("estaduais/CE/leis/lei-1.md", _options).Should().BeFalse();
    }

    [Fact]
    public void PrioridadeColocaAConstituicaoELeisAntesDoVolume()
    {
        var ordenados = new[]
        {
            "reguladores/rfb/x.md", "federal/decretos/2001/d.md", "jurisprudencia/stf/sumulas/s.md",
            "constituicao.md", "federal/leis-ordinarias/2002/l.md", "tratados/oit/t.md",
        }.OrderBy(NormaFilePolicy.Prioridade).ToList();

        ordenados[0].Should().Be("constituicao.md");
        ordenados[1].Should().Be("federal/leis-ordinarias/2002/l.md");
        ordenados[^1].Should().Be("reguladores/rfb/x.md");
    }

    [Fact]
    public void FiltrosDeRecorte_ConsideramUfMunicipioTipoEPeriodo()
    {
        var meta = new NormaMetadata(
            "T", "Lei Ordinária", "1", 2010, null, null, null, "Vigente", false, "v", null, "", "estadual", "BA", null, null, null, [], Colecoes.LegislacaoEstadual);

        NormaFilePolicy.MotivoDeRecusa(meta, new NormasFiltroOptions { Ufs = ["CE"] }).Should().Contain("UF");
        NormaFilePolicy.MotivoDeRecusa(meta, new NormasFiltroOptions { Ufs = ["BA"] }).Should().BeNull();
        NormaFilePolicy.MotivoDeRecusa(meta, new NormasFiltroOptions { Tipos = ["decreto"] }).Should().Contain("tipo");
        NormaFilePolicy.MotivoDeRecusa(meta, new NormasFiltroOptions { AnoDe = 2011 }).Should().Contain("período");
        NormaFilePolicy.MotivoDeRecusa(meta with { Esfera = "federal", Uf = null }, new NormasFiltroOptions { Ufs = ["CE"] })
            .Should().BeNull("norma federal não é restringida por UF");
    }
}

public class NormaChunkerTests
{
    private readonly NormaChunker _chunker = new(new ChunkingOptions());

    private static NormaMetadata Meta(string titulo = "Lei nº 10.406 de 10/01/2002", string? ementa = "Institui o Código Civil.") =>
        new(titulo, "Lei Ordinária", "10406", 2002, new DateOnly(2002, 1, 10), null, ementa, "Vigente", false, "compilada",
            null, "https://normas.leg.br/x", "federal", null, null, null, "civel", [], Colecoes.LeisFederais);

    private static string Lorem(int chars) => string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet consectetur. ", (chars / 40) + 1))[..chars];

    [Fact]
    public void ArtigoCurto_UmChunkPorArtigo_ComTituloEmentaHierarquiaEReferencia()
    {
        var chunks = _chunker.Dividir(Meta(), """
            # Lei nº 10.406 de 10/01/2002

            LEI Nº 10.406

            Institui o Código Civil.

            ## PARTE GERAL

            ### TÍTULO I — DAS PESSOAS NATURAIS

            #### CAPÍTULO I — DA PERSONALIDADE E DA CAPACIDADE

            **Art. 1º** Toda pessoa é capaz de direitos e deveres na ordem civil.

            **Art. 2º** A personalidade civil da pessoa começa do nascimento com vida.
            """);

        var artigos = chunks.Where(c => c.Artigo is not null).ToList();
        artigos.Select(c => c.Artigo).Should().Equal("1º", "2º");
        artigos[0].Texto.Should().StartWith("Lei nº 10.406 de 10/01/2002 — Art. 1º");
        artigos[0].Texto.Should().Contain("PARTE GERAL > TÍTULO I — DAS PESSOAS NATURAIS > CAPÍTULO I — DA PERSONALIDADE E DA CAPACIDADE");
        artigos[0].Texto.Should().Contain("Ementa: Institui o Código Civil.");
        artigos[0].Texto.Should().Contain("Toda pessoa é capaz de direitos e deveres na ordem civil.");
        artigos[0].Texto.Should().NotContain("Art. 2º", "um artigo não vaza para o chunk do outro");
        artigos[0].Secao.Should().Contain("CAPÍTULO I");
        artigos.Should().OnlyContain(c => c.Hash.Length == 64 && c.Tokens > 0);
    }

    [Fact]
    public void ArtigoSoRevogado_NaoViraChunk_MasArtigoComTextoViraMesmoComNotas()
    {
        var chunks = _chunker.Dividir(Meta(), """
            **Art. 3º** (Revogado pela Lei nº 13.146, de 6/7/2015).

            **Art. 4º** São incapazes, relativamente a certos atos: *(Redação dada pela Lei nº 13.146, de 2015)*

            I - os maiores de dezesseis e menores de dezoito anos;
            """);

        chunks.Select(c => c.Artigo).Should().Equal("4º");
    }

    [Fact]
    public void ArtigoLongo_DivideNosParagrafos_RepeteOCaputEPreservaORotuloDoParagrafo()
    {
        var corpo = "**Art. 10.** Caput do artigo dez.\n"
            + string.Join('\n', Enumerable.Range(1, 6).Select(i => $"§ {i}º {Lorem(2000)}"));

        var chunks = _chunker.Dividir(Meta(), corpo).Where(c => c.Artigo == "10").ToList();

        chunks.Count.Should().BeGreaterThanOrEqualTo(4);
        chunks.Should().OnlyContain(c => c.Tokens <= 900, "cada chunk respeita o teto de tokens");
        chunks[0].Paragrafo.Should().BeNull("o primeiro grupo começa no caput");
        chunks[1].Paragrafo.Should().StartWith("§ ");
        chunks[1].Texto.Should().Contain("(caput) Art. 10. Caput do artigo dez.");
        chunks[1].Texto.Should().Contain("Art. 10 — § ");
        chunks.Skip(1).Should().OnlyContain(c => c.Texto.Contains("§ "), "começam num dispositivo, não no meio dele");
    }

    [Fact]
    public void ParagrafoGigante_DivideEmIncisosEPorFim_EmFrases()
    {
        var incisos = string.Join('\n', new[] { "I", "II", "III", "IV", "V" }.Select(n => $"{n} - {Lorem(1800)}"));
        var semEstrutura = Lorem(5000);

        var porInciso = _chunker.Dividir(Meta(), $"**Art. 20.** Caput.\n§ 1º Consideram-se:\n{incisos}").Where(c => c.Artigo == "20").ToList();
        var porFrase = _chunker.Dividir(Meta(), $"**Art. 21.** {semEstrutura}").Where(c => c.Artigo == "21").ToList();

        porInciso.Should().HaveCountGreaterThan(1);
        porInciso.Should().Contain(c => c.Inciso is "I" or "II" or "III" or "IV" or "V");
        porFrase.Should().HaveCountGreaterThan(1);
        porFrase.Should().OnlyContain(c => c.Tokens <= 900);
    }

    [Fact]
    public void DocumentoSemArtigos_Sumula_VirraUmChunkSoComOTitulo()
    {
        var chunks = _chunker.Dividir(Meta("Súmula nº 1 do STF", null), """
            # Súmula nº 1 do STF

            SÚMULA 1
            É vedada a expulsão de estrangeiro casado com Brasileira, ou que tenha filho Brasileiro.

            Data de Aprovação
            Sessão Plenária de 13/12/1963
            """);

        chunks.Should().ContainSingle();
        chunks[0].Artigo.Should().BeNull();
        chunks[0].Texto.Should().StartWith("Súmula nº 1 do STF");
        chunks[0].Texto.Should().Contain("É vedada a expulsão de estrangeiro");
    }

    [Fact]
    public void BlocoJsonDoCnj_VirraSoOsCamposTextuaisLongos()
    {
        var chunks = _chunker.Dividir(Meta("ADPF nº 1031 do STF", null), """
            # ADPF nº 1031 do STF

            ## Registro original do CNJ

            ```json
            {
              "cautelar": "NAO",
              "id": "stf-adpf-1031",
              "linkDecisao": "https://redir.stf.jus.br/paginadorpub/paginador.jsp?docTP=TP&docID=771375428",
              "textoEmenta": "EMENTA AÇÃO DIRETA DE INCONSTITUCIONALIDADE. FEDERALISMO. SISTEMA DE DISTRIBUIÇÃO DE COMPETÊNCIAS."
            }
            ```
            """);

        var texto = string.Join('\n', chunks.Select(c => c.Texto));
        texto.Should().Contain("Texto ementa: EMENTA AÇÃO DIRETA DE INCONSTITUCIONALIDADE");
        texto.Should().NotContain("cautelar").And.NotContain("redir.stf.jus.br").And.NotContain("```");
    }

    [Fact]
    public void Adct_ReiniciaANumeracaoEApareceNaHierarquia()
    {
        var chunks = _chunker.Dividir(Meta("Constituição de 1988", null), """
            **Art. 1º** A República Federativa do Brasil constitui-se em Estado Democrático de Direito.

            ## ATO DAS DISPOSIÇÕES CONSTITUCIONAIS TRANSITÓRIAS

            **Art. 1º** O Presidente da República, o Presidente do Supremo Tribunal Federal e os membros do Congresso Nacional prestarão o compromisso.
            """);

        var artigos = chunks.Where(c => c.Artigo == "1º").ToList();
        artigos.Should().HaveCount(2);
        artigos[0].Texto.Should().NotContain("ATO DAS DISPOSIÇÕES");
        artigos[1].Texto.Should().Contain("ATO DAS DISPOSIÇÕES CONSTITUCIONAIS TRANSITÓRIAS");
    }

    [Fact]
    public void ReprocessarAMesmaEntrada_DaOsMesmosHashes()
    {
        const string corpo = "**Art. 1º** Texto um.\n\n**Art. 2º** Texto dois.";

        _chunker.Dividir(Meta(), corpo).Select(c => c.Hash)
            .Should().Equal(_chunker.Dividir(Meta(), corpo).Select(c => c.Hash));
    }

    [Theory]
    [InlineData("Art. 121-A. Texto.", "121-A")]
    [InlineData("Art. 1.234 Texto.", "1.234")]
    [InlineData("Artigo 5º Texto.", "5º")]
    public void ReconheceOsFormatosDeArtigo(string linha, string esperado)
    {
        var chunks = _chunker.Dividir(Meta(), linha + " Mais algum texto do dispositivo para ter conteúdo.");

        chunks.Should().ContainSingle(c => c.Artigo == esperado);
    }
}

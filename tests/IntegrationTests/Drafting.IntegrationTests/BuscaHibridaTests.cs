using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Conversao;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Infrastructure.Qdrant;
using Advocacia.Modules.Drafting.Infrastructure.Storage;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.IntegrationTests;

/// <summary>Qdrant real (Testcontainers). Sem Docker: aponte <c>ADVOCACIA_TEST_QDRANT</c> para um Qdrant descartável.</summary>
public sealed class QdrantFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Url { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("ADVOCACIA_TEST_QDRANT") is { Length: > 0 } externo)
        {
            Url = externo;

            return;
        }

        _container = new ContainerBuilder()
            .WithImage("qdrant/qdrant:v1.15.5")
            .WithPortBinding(6333, true)
            .WithPortBinding(6334, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(6333).ForPath("/readyz")))
            .Build();
        await _container.StartAsync();
        Url = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(6334)}";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Apaga as coleções: testes que medem ranking não podem ver o acervo um do outro.</summary>
    public async Task LimparAsync()
    {
        using var conexao = Conectar();
        foreach (var colecao in Colecoes.Todas)
        {
            if (await conexao.Cliente.CollectionExistsAsync(colecao))
            {
                await conexao.Cliente.DeleteCollectionAsync(colecao);
            }
        }
    }

    public QdrantConexao Conectar() =>
        new(Options.Create(new QdrantOptions { BaseUrl = Url, AllowInsecureHttp = true }), NullLogger<QdrantConexao>.Instance);
}

[CollectionDefinition(Name)]
public sealed class BuscaCollection : ICollectionFixture<PostgresFixture>, ICollectionFixture<QdrantFixture>
{
    public const string Name = "drafting-busca";
}

/// <summary>
/// Embedding determinístico de mentira (saco de palavras por hash): aproxima textos que compartilham termos.
/// Serve para exercitar a infraestrutura; NÃO mede a qualidade semântica do BGE-M3.
/// </summary>
internal sealed class EmbeddingPorHash : IEmbeddingService
{
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(Vetor).ToList());

    public static float[] Vetor(string texto)
    {
        var v = new float[EmbeddingConstants.Dimensoes];
        foreach (var indice in Bm25.Documento(texto).Indices)
        {
            v[indice % EmbeddingConstants.Dimensoes] += 1;
        }

        var norma = (float)Math.Sqrt(v.Sum(x => x * x));

        return norma == 0 ? [1, .. v[1..]] : [.. v.Select(x => x / norma)];
    }
}

internal sealed class Relogio : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

[Collection(BuscaCollection.Name)]
public class BuscaHibridaTests(PostgresFixture postgres, QdrantFixture qdrant)
{
    [Fact]
    public async Task AsCincoColecoes_SaoCriadas_UmaVezSo()
    {
        using var conexao = qdrant.Conectar();
        using var servico = new QdrantService(conexao, NullLogger<QdrantService>.Instance);

        await servico.GarantirColecoesAsync();
        using var outro = new QdrantService(conexao, NullLogger<QdrantService>.Instance);
        await outro.GarantirColecoesAsync();

        (await servico.EstadoAsync()).Where(c => c.Existe).Select(c => c.Nome).Should()
            .BeEquivalentTo("constituicao_federal", "leis_federais", "jurisprudencia", "documentos_caso", "pecas_modelo");
        (await servico.VerificarSaudeAsync()).Should().BeTrue();
    }

    private static CasoVectorPoint Ponto(
        Guid tenant, Guid caso, string texto, Guid? documento = null, Guid? usuario = null, bool restrito = false, bool suspeito = false)
    {
        var doc = documento ?? Guid.NewGuid();

        return new CasoVectorPoint(
            Guid.NewGuid(), EmbeddingPorHash.Vetor(texto), Bm25.Documento(texto), texto,
            new ChunkMetadata(
                doc, "a.pdf", 1, null, null, null, null, null, TextoHash.DoTexto(texto),
                Caso: new CasoRef(tenant, caso, Guid.NewGuid(), doc, usuario ?? Guid.NewGuid(), OrigemTexto.Nativo, null, null, restrito, suspeito)),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Anexos_OBancoVetorialSoDevolveOCasoPedido_NasDuasBuscas()
    {
        using var conexao = qdrant.Conectar();
        using var servico = new QdrantService(conexao, NullLogger<QdrantService>.Instance);
        await servico.GarantirColecoesAsync();
        var store = new CasoQdrantStore(conexao);
        var (tenant, casoA, casoB, usuario) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        const string texto = "multa contratual por atraso no pagamento";
        await store.UpsertAsync(
        [
            Ponto(tenant, casoA, texto + " do caso A"),
            Ponto(tenant, casoB, texto + " do caso B"),
            Ponto(Guid.NewGuid(), casoA, texto + " de outro escritório com o mesmo id de caso"),
            Ponto(tenant, casoA, texto + " restrito de colega", restrito: true),
            Ponto(tenant, casoA, texto + " restrito meu", usuario: usuario, restrito: true),
            Ponto(tenant, casoA, texto + " suspeito", suspeito: true),
        ]);

        async Task<IEnumerable<string>> Buscar(bool veRestritos)
        {
            var filtro = new FiltroCaso(tenant, casoA, usuario, veRestritos);
            var densos = await store.BuscarDensoAsync(filtro, EmbeddingPorHash.Vetor(texto), 50);
            var bm25 = await store.BuscarBm25Async(filtro, Bm25.Consulta("multa contratual"), 50);
            bm25.Select(h => h.Texto).Should().BeEquivalentTo(densos.Select(h => h.Texto));

            return densos.Select(h => h.Texto[texto.Length..].Trim());
        }

        (await Buscar(veRestritos: false)).Should().BeEquivalentTo("do caso A", "restrito meu");
        (await Buscar(veRestritos: true)).Should().BeEquivalentTo("do caso A", "restrito meu", "restrito de colega");
    }

    [Fact]
    public async Task RemoverDocumento_ApagaSoDaqueleCaso_MesmoComOMesmoIdDeDocumento()
    {
        using var conexao = qdrant.Conectar();
        using var servico = new QdrantService(conexao, NullLogger<QdrantService>.Instance);
        await servico.GarantirColecoesAsync();
        var store = new CasoQdrantStore(conexao);
        var (tenant, casoA, casoB, documento, usuario) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await store.UpsertAsync([Ponto(tenant, casoA, "recibo", documento), Ponto(tenant, casoB, "recibo", documento)]);

        await store.RemoverDocumentoAsync(tenant, casoA, documento);

        (await store.BuscarBm25Async(new FiltroCaso(tenant, casoA, usuario, true), Bm25.Consulta("recibo"), 5)).Should().BeEmpty();
        (await store.BuscarBm25Async(new FiltroCaso(tenant, casoB, usuario, true), Bm25.Consulta("recibo"), 5)).Should().HaveCount(1);
    }

    private static readonly (string Caminho, string Conteudo)[] Acervo =
    [
        ("constituicao.md", "# Constituição da República Federativa do Brasil de 1988\n\n> Fonte: [x](https://www.planalto.gov.br/cf)\n\n## TÍTULO II — Dos Direitos\n\n**Art. 5º** Todos são iguais perante a lei, assegurada a indenização por dano moral.\n\n# ATO DAS DISPOSIÇÕES CONSTITUCIONAIS TRANSITÓRIAS\n\n**Art. 5º** Não se aplicam às eleições previstas as regras transitórias.\n"),
        ("federal/leis-ordinarias/2002/lei-10406-2002-01-10.md", Lei("Lei nº 10.406 de 10/01/2002", "10406", "2002-01-10", "urn:lex:br:federal:lei:2002-01-10;10406", "Institui o Código Civil.",
            "**Art. 186.** Aquele que, por ação ou omissão voluntária, violar direito e causar dano a outrem, comete ato ilícito.\n\n**Art. 389.** Não cumprida a obrigação, responde o devedor por perdas e danos, mais juros e atualização monetária.\n\n**Art. 927.** Aquele que, por ato ilícito, causar dano a outrem, fica obrigado a repará-lo.")),
        ("federal/leis-ordinarias/1990/lei-8078-1990-09-11.md", Lei("Lei nº 8.078 de 11/09/1990", "8078", "1990-09-11", "urn:lex:br:federal:lei:1990-09-11;8078", "Dispõe sobre a proteção do consumidor.",
            "**Art. 14.** O fornecedor de serviços responde, independentemente da existência de culpa, pela reparação dos danos causados aos consumidores.")),
        ("federal/leis-ordinarias/1965/lei-4591-1965-01-01.md", Lei("Lei nº 4.591 de 01/01/1965", "4591", "1965-01-01", null, "Norma antiga.", "**Art. 1º** Norma revogada sobre dano.", "true")),
        ("federal/decretos-lei/1940/del-2848-1940-12-07.md", Lei("Decreto-Lei nº 2.848 de 07/12/1940", "2848", "1940-12-07", null, "Código Penal.", "**Art. 121.** Matar alguém.", tipo: "Decreto-Lei")),
        ("jurisprudencia/stj/sumulas/sumula-297.md", "---\ntitulo: \"Súmula nº 297 do STJ\"\ntribunal: \"STJ\"\ntipo: \"Súmula\"\nnumero: \"297\"\ndata: 2004-05-12\nrevogada: false\nfonte: \"https://www.stj.jus.br/s.pdf\"\n---\n# Súmula nº 297 do STJ\n\nO Código de Defesa do Consumidor é aplicável às instituições financeiras.\n"),
    ];

    private static string Lei(string titulo, string numero, string data, string? urn, string ementa, string corpo, string revogada = "false", string tipo = "Lei Ordinária") =>
        $"---\ntitulo: \"{titulo}\"\ntipo: \"{tipo}\"\nnumero: \"{numero}\"\ndata: {data}\nementa: \"{ementa}\"\nrevogada: {revogada}\nversao_texto: \"compilada\"\n{(urn is null ? string.Empty : $"urn: \"{urn}\"\n")}fonte: \"https://normas.leg.br/{numero}\"\n---\n# {titulo}\n\n{corpo}\n";

    /// <summary>Ingestão real (arquivo → PostgreSQL → Qdrant) e busca real, com as três coleções.</summary>
    [Fact]
    public async Task IngestaoEBusca_PontaAPonta_AchaODispositivoCerto_PorSemanticaPorTermoEPorReferencia()
    {
        await qdrant.LimparAsync();
        var (dono, _) = await postgres.NewMigratedDatabaseAsync();
        DraftingDbContext Contexto() => new(
            new DbContextOptionsBuilder<DraftingDbContext>().UseNpgsql(dono).UseSnakeCaseNamingConvention().Options, new AsyncLocalTenantContext());
        using var conexao = qdrant.Conectar();
        using var qdrantService = new QdrantService(conexao, NullLogger<QdrantService>.Instance);
        var vetores = new NormaQdrantStore(conexao);
        var dir = Directory.CreateTempSubdirectory("acervo-it-").FullName;
        try
        {
            var opcoes = Options.Create(new NormasIngestaoOptions
            {
                SomenteAltaUtilizacao = false, Storage = { DiretorioLocal = Path.Combine(dir, "storage") },
            });

            async Task<IngestaoArquivoResultado> Ingerir(string caminho, string conteudo)
            {
                var arquivo = Path.Combine(dir, caminho);
                Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
                await File.WriteAllTextAsync(arquivo, conteudo);
                await using var db = Contexto();

                return await new NormaIngestionService(
                        new NormaRepository(db), new LocalObjectStorage(opcoes), new EmbeddingPorHash(), vetores, qdrantService,
                        new ChunkerJuridicoService(Options.Create(new ChunkingOptions())), opcoes,
                        new GpuStateService(Options.Create(new VastAiOptions())), new Relogio(), NullLogger<NormaIngestionService>.Instance)
                    .IngerirAsync(new RepoArquivo(caminho, arquivo, new FileInfo(arquivo).Length), "c1");
            }

            foreach (var (caminho, conteudo) in Acervo)
            {
                (await Ingerir(caminho, conteudo)).Resultado.Should().Be(IngestaoResultado.Novo, caminho);
            }

            (await Ingerir(Acervo[1].Caminho, Acervo[1].Conteudo)).Resultado.Should().Be(IngestaoResultado.SemMudanca);

            async Task<IReadOnlyList<VectorHit>> Buscar(string tese, string area = "civel", DateOnly? fatos = null)
            {
                await using var db = Contexto();
                var consulta = new ConsultaPreparada(
                    new ConsultaEstruturada("peticao_inicial", area, tese, [], [], [], null, tese, string.Empty, [], [], [], "teste"),
                    [EmbeddingPorHash.Vetor(tese)], tese, ExtratorReferencias.Extrair(tese), null, fatos);

                return await new BuscaJuridicaService(
                    vetores, new NormaRepository(db), new RerankerService(), Options.Create(new BuscaNormasOptions()), NullLogger<BuscaJuridicaService>.Instance)
                    .BuscarAsync(consulta);
            }

            static string Rotulo(VectorHit h) => CitationLabel.For(h.Meta);

            Rotulo((await Buscar("não cumprida a obrigação, responde o devedor por perdas e danos"))[0]).Should().Be("Código Civil, art. 389");
            Rotulo((await Buscar("aplicação do art. 927 do Código Civil"))[0]).Should().Be("Código Civil, art. 927");
            Rotulo((await Buscar("instituições financeiras e Súmula 297 do STJ", "consumidor"))[0]).Should().Be("STJ, Súmula nº 297");
            Rotulo((await Buscar("fornecedor responde por danos aos consumidores independentemente de culpa", "consumidor"))[0])
                .Should().Be("Código de Defesa do Consumidor, art. 14");

            // "Art. 5º da CF" é o do corpo permanente, não o do ADCT.
            var cf = (await Buscar("art. 5º da Constituição Federal")).First(h => h.Meta.Norma!.Colecao == Colecoes.ConstituicaoFederal);
            Rotulo(cf).Should().Be("Constituição Federal, art. 5º");

            // Filtro de área: penal não aparece em caso cível; em caso penal, aparece.
            (await Buscar("matar alguém")).Select(Rotulo).Should().NotContain("Código Penal, art. 121");
            (await Buscar("matar alguém", "penal")).Select(Rotulo).Should().Contain("Código Penal, art. 121");

            // Revogada não é descartada: vem marcada.
            (await Buscar("norma antiga revogada sobre dano")).Should().Contain(h => h.Meta.Norma!.Revogada);

            // Versão nova substitui a antiga no índice, sem deixar rastro.
            (await Ingerir(Acervo[1].Caminho, Acervo[1].Conteudo.Replace("perdas e danos, mais juros", "perdas e danos, REDAÇÃO NOVA, mais juros"))).Resultado
                .Should().Be(IngestaoResultado.Atualizado);
            var depois = (await Buscar("não cumprida a obrigação, responde o devedor por perdas e danos")).Where(h => h.Meta.Norma!.Artigo == "389").ToList();
            depois.Should().ContainSingle().Which.Texto.Should().Contain("REDAÇÃO NOVA");
            depois[0].Meta.Norma!.VersaoNumero.Should().Be(2);

            await using (var db = Contexto())
            {
                var repository = new NormaRepository(db);
                (await repository.UrnExisteAsync("urn:lex:br:federal:lei:2002-01-10;10406")).Should().BeTrue();
                (await repository.UrnExisteAsync("urn:lex:inventada")).Should().BeFalse();

                // O full-text de reserva também funciona no banco real.
                (await repository.BuscarPalavrasChaveAsync(new NormaFiltro(Areas: AreaMapper.Compativeis("consumidor")), "instituições financeiras", 10))
                    .Should().Contain(h => h.Meta.Norma!.Numero == "297");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

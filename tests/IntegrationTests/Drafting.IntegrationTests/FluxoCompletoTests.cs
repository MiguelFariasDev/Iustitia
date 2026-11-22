using System.Runtime.CompilerServices;
using System.Text;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Advocacia.BuildingBlocks.Infrastructure.Security;
using Advocacia.BuildingBlocks.Infrastructure.Tenancy;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Normas;
using Advocacia.Modules.Drafting.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Advocacia.Modules.Drafting.IntegrationTests;

/// <summary>
/// O produto inteiro com o contêiner de produção: API (serviços) → fila no PostgreSQL → coordenador do worker →
/// conversão, indexação no Qdrant, ingestão do acervo, geração, conferência, revisão e exportação. A conexão é a
/// de <c>app_user</c>, então o RLS vale em cada passo. Só o modelo (texto, embeddings) e a GPU são de mentira.
/// </summary>
[Collection(BuscaCollection.Name)]
public sealed class FluxoCompletoTests(PostgresFixture postgres, QdrantFixture qdrant) : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("fluxo-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class Usuario : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId { get; set; }

        public Guid? TenantId { get; set; }

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => ["Lawyer"];

        public bool IsInRole(string role) => role == "Lawyer";
    }

    private sealed class SemAlertas : IAlertPublisher
    {
        public Task<bool> PublishAsync(AlertRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class GpuPronta : IGpuOrchestrator
    {
        public Task<Result> GarantirProntaAsync(string motivo, Func<CancellationToken, Task>? aoIniciar, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<bool> PararSeOciosaAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    /// <summary>Redige citando o primeiro anexo e o primeiro artigo que recebeu no prompt — e inventa uma súmula.</summary>
    private sealed class ModeloDeMentira : ILlmService, ILlmEstruturado
    {
        public string ModelName => "jurema-7b";

        public string? UltimoPrompt { get; private set; }

        public async IAsyncEnumerable<LlmChunk> ChatStreamAsync(
            LlmChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            UltimoPrompt = request.Messages[1].Content;
            foreach (var parte in new[]
                     {
                         "DOS FATOS\nO contrato previa R$ 50.000,00 [contrato.pdf, p. 2].\n",
                         "DO DIREITO\nResponde o devedor por perdas e danos [Código Civil, art. 389], conforme a Súmula 12345 do STJ.\n",
                         "FONTES UTILIZADAS\n-\nPONTOS PARA REVISÃO HUMANA\n-",
                     })
            {
                await Task.Yield();
                yield return new LlmChunk(parte, false, null, null);
            }

            yield return new LlmChunk(string.Empty, true, 900, 300);
        }

        public Task<string> CompletarJsonAsync(string system, string user, string jsonSchema, int maxTokens, CancellationToken cancellationToken = default) =>
            Task.FromResult(jsonSchema.Contains("tipoDocumento", StringComparison.Ordinal)
                ? """{"tipoDocumento":"contrato","area":"civel","valores":["R$ 50.000,00","R$ 1,00"],"resumo":"Contrato.","confianca":0.9}"""
                : """{"tese":"inadimplemento contratual","temas":["perdas e danos","contrato"],"dispositivosSuspeitos":["Código Civil","art. 389"],"tribunalPreferido":["STJ"],"riscos":[],"lacunas":["data da mora"]}""");
    }

    private ServiceProvider Montar(string conexao, ModeloDeMentira modelo)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = conexao,
            ["Drafting:Enabled"] = "true",
            ["Drafting:Fila:ParcialIntervaloMs"] = "0",
            ["Qdrant:BaseUrl"] = qdrant.Url,
            ["Qdrant:AllowInsecureHttp"] = "true",
            ["Ingestao:DiretorioLocal"] = Path.Combine(_dir, "leis"),
            ["Ingestao:SomenteAltaUtilizacao"] = "true",
            ["Ingestao:Storage:DiretorioLocal"] = Path.Combine(_dir, "storage"),
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAlertPublisher, SemAlertas>();
        services.AddSingleton<IDateTimeProvider, Relogio>();
        services.AddSingleton<ITenantContext, AsyncLocalTenantContext>();
        services.AddScoped<Usuario>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<Usuario>());
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<TenantContextInterceptor>();
        services.AddSingleton(Options.Create(new FieldEncryptionOptions
        {
            ActiveKeyId = "t1", Keys = new() { ["t1"] = Convert.ToBase64String(new byte[32]) }, BlindIndexKey = Convert.ToBase64String(new byte[32]),
        }));
        services.AddDraftingInfrastructure(configuration);
        services.Replace(ServiceDescriptor.Singleton<ILlmService>(modelo));
        services.Replace(ServiceDescriptor.Singleton<ILlmEstruturado>(modelo));
        services.Replace(ServiceDescriptor.Singleton<IEmbeddingService, EmbeddingPorHash>());
        services.Replace(ServiceDescriptor.Scoped<IGpuOrchestrator, GpuPronta>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Uma "requisição": escopo próprio, usuário e tenant do token, isolada num fluxo assíncrono.</summary>
    private static Task<T> Como<T>(ServiceProvider provider, Guid tenant, Guid usuario, Func<IServiceProvider, Task<T>> acao) =>
        Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant);
            var atual = scope.ServiceProvider.GetRequiredService<Usuario>();
            (atual.TenantId, atual.UserId) = (tenant, usuario);

            return await acao(scope.ServiceProvider);
        });

    private static byte[] Pdf()
    {
        var builder = new PdfDocumentBuilder();
        var fonte = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var linhas in new[]
                 {
                     new[] { "CONTRATO DE PRESTACAO DE SERVICOS", "CONTRATANTE: Empresa X Ltda", "CONTRATADA: Maria Souza" },
                     ["CLAUSULA 5 - PAGAMENTO", "A contratante pagara o valor de R$ 50.000,00 ate 10/03/2024,", "sob pena de multa de 10% por inadimplemento contratual."],
                 })
        {
            var pagina = builder.AddPage(595, 842);
            for (var i = 0; i < linhas.Length; i++)
            {
                pagina.AddText(linhas[i], 12, new PdfPoint(40, 780 - (20 * i)), fonte);
            }
        }

        return builder.Build();
    }

    [Fact]
    public async Task DoAnexoAPecaExportada_ComOWorkerReal_SobRls()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "leis/federal/leis-ordinarias/2002"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "leis/constituicao.md"), "# Constituição da República Federativa do Brasil de 1988\n\n**Art. 5º** Todos são iguais perante a lei.\n");
        await File.WriteAllTextAsync(
            Path.Combine(_dir, "leis/federal/leis-ordinarias/2002/lei-10406-2002-01-10.md"),
            "---\ntitulo: \"Lei nº 10.406 de 10/01/2002\"\ntipo: \"Lei Ordinária\"\nnumero: \"10406\"\ndata: 2002-01-10\nrevogada: false\nurn: \"urn:lex:br:federal:lei:2002-01-10;10406\"\nfonte: \"https://normas.leg.br/x\"\n---\n# Lei nº 10.406 de 10/01/2002\n\n**Art. 389.** Não cumprida a obrigação, responde o devedor por perdas e danos.\n\n**Art. 186.** Comete ato ilícito quem causar dano a outrem.\n");

        await qdrant.LimparAsync();
        var (_, appUser) = await postgres.NewMigratedDatabaseAsync();
        var modelo = new ModeloDeMentira();
        await using var provider = Montar(appUser, modelo);
        var coordenador = provider.GetRequiredService<FilaCoordinator>();
        var (tenant, dona, tenantB, outra) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Acervo: ingestão completa, depois incremental sem nada a fazer.
        var orquestrador = provider.GetRequiredService<INormaSyncOrchestrator>();
        var primeira = await orquestrador.SincronizarAsync(IngestaoJobTipo.Completa);
        (primeira.Novos, primeira.Erros).Should().Be((2, 0));
        (await orquestrador.SincronizarAsync(IngestaoJobTipo.Incremental)).SemMudanca.Should().Be(2);

        // Caso e anexo.
        var caso = (await Como(provider, tenant, dona, sp => sp.GetRequiredService<ICasoService>()
            .CriarAsync(new CriarCasoRequest(Guid.NewGuid(), "Cobrança", "civel", "Empresa X")))).Value.Id;
        var pdf = Pdf();
        var anexo = (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IAnexoService>()
            .EnviarAsync(caso, new MemoryStream(pdf), "contrato.pdf", pdf.Length, false))).Value.Id;

        // Outro escritório, com um caso parecido: não pode vazar nada para cá.
        var casoB = (await Como(provider, tenantB, outra, sp => sp.GetRequiredService<ICasoService>()
            .CriarAsync(new CriarCasoRequest(Guid.NewGuid(), "Outro", "civel")))).Value.Id;
        var segredo = Encoding.UTF8.GetBytes("SEGREDO DO OUTRO ESCRITÓRIO: inadimplemento contratual, perdas e danos, R$ 777,00.");
        await Como(provider, tenantB, outra, sp => sp.GetRequiredService<IAnexoService>()
            .EnviarAsync(casoB, new MemoryStream(segredo), "segredo.txt", segredo.Length, false));

        // Worker: conversão enfileira indexação; dois ciclos esvaziam a fila.
        await coordenador.RunCycleAsync(CancellationToken.None);
        await coordenador.RunCycleAsync(CancellationToken.None);
        var lido = (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IAnexoService>().ObterAsync(caso, anexo))).Value;
        lido.Status.Should().Be("Indexado", lido.Mensagem);
        (lido.Paginas, lido.TipoDocumento).Should().Be((2, "contrato"));
        lido.Classificacao!.Valores.Should().Equal("R$ 50.000,00");
        Directory.EnumerateFiles(Path.Combine(_dir, "storage", "casos"), "*", SearchOption.AllDirectories)
            .Should().NotBeEmpty().And.OnlyContain(f => !Encoding.Latin1.GetString(File.ReadAllBytes(f)).Contains("CLAUSULA"), "no storage só há bytes cifrados");

        // Minuta.
        var peca = (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IGeracaoPecaService>().SubmeterAsync(
            new GerarPecaRequest("peticao_inicial", "civel", "A contratante não pagou o combinado.", "Condenação em perdas e danos.", CasoId: caso, DataFatos: new DateOnly(2024, 3, 10))))).Value.Id;
        await coordenador.RunCycleAsync(CancellationToken.None);
        var detalhe = (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IGeracaoPecaService>().ObterAsync(peca))).Value;

        detalhe.Status.Should().Be("Concluido", string.Join('\n', detalhe.Avisos));
        modelo.UltimoPrompt.Should().Contain("[contrato.pdf, p. 2]").And.Contain("[Código Civil, art. 389]").And.Contain("CLAUSULA 5").And.NotContain("SEGREDO");
        detalhe.Fontes.Should().Contain(f => f.Rotulo == "Código Civil, art. 389" && f.Citada && f.UrlOrigem == "https://normas.leg.br/x");
        detalhe.Fontes.Should().Contain(f => f.Rotulo == "contrato.pdf, p. 2" && f.Citada && f.DocumentoOrigemId == anexo);
        detalhe.Citacoes!.Where(c => c.Bloqueante).Should().ContainSingle().Which.Texto.Should().Contain("12345");
        detalhe.Avisos.Should().Contain(a => a.Contains("data da mora"));
        detalhe.ExportacaoBloqueada.Should().BeTrue();

        // Outro escritório não enxerga nem o caso, nem a peça.
        (await Como(provider, tenantB, outra, sp => sp.GetRequiredService<IGeracaoPecaService>().ObterAsync(peca))).Error.Code
            .Should().Be(nameof(ErrorCode.DRAFTING_REQUEST_NOT_FOUND));
        (await Como(provider, tenantB, outra, sp => sp.GetRequiredService<ICasoService>().ObterAsync(caso))).IsFailure.Should().BeTrue();

        // Revisão humana e exportação.
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IRevisaoPecaService>().ExportarAsync(peca, "docx"))).IsFailure.Should().BeTrue();
        var duvidosa = detalhe.Citacoes!.Single(c => c.Bloqueante).Id;
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IRevisaoPecaService>()
            .DecidirAsync(peca, duvidosa, new DecidirCitacaoRequest("rejeitada", "Súmula inexistente.")))).IsSuccess.Should().BeTrue();
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IRevisaoPecaService>()
            .ConcluirAsync(peca, new ConcluirRevisaoRequest(true, "Texto final sem a súmula.")))).IsSuccess.Should().BeTrue();
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IRevisaoPecaService>().ExportarAsync(peca, "docx"))).Value.Conteudo
            .Take(2).Should().Equal((byte)'P', (byte)'K');

        // Painel, reprocessamento e remoção (inclusive do índice).
        var metricas = (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IPecaMetricasService>().ObterAsync())).Value;
        (metricas.Total, metricas.Concluidas, metricas.AguardandoRevisao).Should().Be((1, 1, 0));
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IAnexoService>().ReprocessarAsync(caso, anexo))).IsSuccess.Should().BeTrue();
        await coordenador.RunCycleAsync(CancellationToken.None);
        await coordenador.RunCycleAsync(CancellationToken.None);
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IAnexoService>().ObterAsync(caso, anexo))).Value.Status.Should().Be("Indexado");
        (await Como(provider, tenant, dona, sp => sp.GetRequiredService<IAnexoService>().RemoverAsync(caso, anexo))).IsSuccess.Should().BeTrue();
        (await provider.GetRequiredService<ICasoVectorStore>().BuscarDensoAsync(new FiltroCaso(tenant, caso, dona, true), EmbeddingPorHash.Vetor("contrato"), 10))
            .Should().BeEmpty();
    }
}
